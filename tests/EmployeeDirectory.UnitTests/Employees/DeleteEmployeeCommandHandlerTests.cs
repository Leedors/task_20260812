using EmployeeDirectory.Application.Employees.Commands.DeleteEmployee;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Domain.Employees;
using EmployeeDirectory.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployeeDirectory.UnitTests.Employees;

public sealed class DeleteEmployeeCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    private readonly FakeEmployeeRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeleteEmployeeCommandHandler _handler;

    public DeleteEmployeeCommandHandlerTests()
        => _handler = new DeleteEmployeeCommandHandler(
            _repository,
            _unitOfWork,
            new FixedDateTimeProvider(Today),
            NullLogger<DeleteEmployeeCommandHandler>.Instance);

    [Fact]
    public async Task 제외하면_삭제_시각이_기록되고_저장된다()
    {
        var id = _repository.Seed(
            Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value);

        var result = await _handler.HandleAsync(new DeleteEmployeeCommand(id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _unitOfWork.SaveCount);

        // 물리 삭제가 아니라 제외 표시다. 조회에서는 사라지지만 행 자체는 남아 있다.
        Assert.Null(await _repository.FindByIdAsync(id, default));
    }

    [Fact]
    public async Task 없는_직원이면_NotFound를_반환한다()
    {
        var result = await _handler.HandleAsync(new DeleteEmployeeCommand(999), default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.not_found", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task 이미_제외된_직원을_다시_제외하면_NotFound다()
    {
        var id = _repository.Seed(
            Employee.Create("김철수", "charles@clovf.com", "01075312468", new DateOnly(2018, 3, 7), Today).Value);

        await _handler.HandleAsync(new DeleteEmployeeCommand(id), default);
        var second = await _handler.HandleAsync(new DeleteEmployeeCommand(id), default);

        Assert.True(second.IsFailure);
        Assert.Equal("employee.not_found", second.FirstError.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 식별자가_유효하지_않으면_검증에_실패한다(int id)
    {
        var validator = new DeleteEmployeeCommandValidator();

        var error = Assert.Single(validator.Validate(new DeleteEmployeeCommand(id)));

        Assert.Equal("employee.id_invalid", error.Code);
    }
}
