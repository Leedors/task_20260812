using EmployeeDirectory.Application.Employees.Commands.UpdateEmployee;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Domain.Employees;
using EmployeeDirectory.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployeeDirectory.UnitTests.Employees;

public sealed class UpdateEmployeeCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    private readonly FakeEmployeeRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateEmployeeCommandHandler _handler;

    public UpdateEmployeeCommandHandlerTests()
        => _handler = new UpdateEmployeeCommandHandler(
            _repository,
            _unitOfWork,
            new FixedDateTimeProvider(Today),
            NullLogger<UpdateEmployeeCommandHandler>.Instance);

    private int SeedEmployee(string name = "김철수", string email = "charles@clovf.com")
        => _repository.Seed(Employee.Create(name, email, "01075312468", new DateOnly(2018, 3, 7), Today).Value);

    [Fact]
    public async Task 모든_필드를_교체한다()
    {
        var id = SeedEmployee();

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "김철수(변경)", "changed@clovf.com", "010-9999-8888", "2019.01.01"),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("김철수(변경)", result.Value.Name);
        Assert.Equal("changed@clovf.com", result.Value.Email);
        Assert.Equal("010-9999-8888", result.Value.Tel);
        Assert.Equal(new DateOnly(2019, 1, 1), result.Value.Joined);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task 없는_직원이면_NotFound를_반환한다()
    {
        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(999, "김철수", "charles@clovf.com", "01075312468", "2018.03.07"),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.not_found", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task 이미_제외된_직원은_찾을_수_없다()
    {
        var id = SeedEmployee();
        var employee = await _repository.FindByIdAsync(id, default);
        employee!.MarkDeleted(DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "김철수", "charles@clovf.com", "01075312468", "2018.03.07"),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.not_found", result.FirstError.Code);
    }

    [Fact]
    public async Task 다른_직원이_쓰는_이메일로_바꾸면_Conflict를_반환한다()
    {
        var id = SeedEmployee();
        SeedEmployee("박영희", "matilda@clovf.com");

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "김철수", "matilda@clovf.com", "01075312468", "2018.03.07"),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.email_taken", result.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task 자기_이메일을_그대로_두는_것은_충돌이_아니다()
    {
        var id = SeedEmployee();

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "김철수", "charles@clovf.com", "010-1111-2222", "2018.03.07"),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("010-1111-2222", result.Value.Tel);
    }

    [Fact]
    public async Task 입사일_형식이_틀리면_허용_형식을_안내한다()
    {
        var id = SeedEmployee();

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "김철수", "charles@clovf.com", "01075312468", "07/03/2018"),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.joined_invalid", result.FirstError.Code);
        Assert.Contains("yyyy-MM-dd", result.FirstError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 검증에_실패하면_저장하지_않는다()
    {
        var id = SeedEmployee();

        var result = await _handler.HandleAsync(
            new UpdateEmployeeCommand(id, "", "이메일아님", "123", "2050.01.01"),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Contains(result.Errors, error => error.Code == "employee.name_required");
        Assert.Contains(result.Errors, error => error.Code == "employee.joined_in_future");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 식별자가_유효하지_않으면_검증에_실패한다(int id)
    {
        var validator = new UpdateEmployeeCommandValidator();

        var error = Assert.Single(validator.Validate(
            new UpdateEmployeeCommand(id, "김철수", "charles@clovf.com", "01075312468", "2018.03.07")));

        Assert.Equal("employee.id_invalid", error.Code);
    }
}
