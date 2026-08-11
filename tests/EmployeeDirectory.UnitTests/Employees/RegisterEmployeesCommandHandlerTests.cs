using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;
using EmployeeDirectory.Domain.Employees;
using EmployeeDirectory.Infrastructure.Parsing;
using EmployeeDirectory.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployeeDirectory.UnitTests.Employees;

public sealed class RegisterEmployeesCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    private readonly FakeEmployeeRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterEmployeesCommandHandler _handler;

    public RegisterEmployeesCommandHandlerTests()
        => _handler = new RegisterEmployeesCommandHandler(
            new EmployeeSourceParserResolver([new JsonEmployeeParser(), new CsvEmployeeParser()]),
            _repository,
            _unitOfWork,
            new FixedDateTimeProvider(Today),
            NullLogger<RegisterEmployeesCommandHandler>.Instance);

    [Fact]
    public async Task csv를_등록하면_신규_건수를_반환한다()
    {
        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           박영희, matilda@clovf.com, 01087654321, 2021.04.28
                           """;

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeSourceFormat.Csv, result.Value.Format);
        Assert.Equal(2, result.Value.Created);
        Assert.Equal(0, result.Value.Updated);
        Assert.Equal(2, result.Value.TotalProcessed);
        Assert.Equal(2, _repository.Added.Count);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task json을_등록하면_신규_건수를_반환한다()
    {
        const string Json = """[{"name":"김클로","email":"clo@clovf.com","tel":"010-1111-2424","joined":"2012-01-05"}]""";

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Json)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeSourceFormat.Json, result.Value.Format);
        Assert.Equal(1, result.Value.Created);
        Assert.Equal("010-1111-2424", Assert.Single(_repository.Added).Tel.Formatted);
    }

    [Fact]
    public async Task 이미_존재하는_이메일은_갱신한다()
    {
        _repository.Seed(Employee.Create("김철수", "charles@clovf.com", "01000000000", new DateOnly(2018, 3, 7), Today).Value);

        const string Csv = "김철수(변경), charles@clovf.com, 010-9999-8888, 2018.03.07";

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Created);
        Assert.Equal(1, result.Value.Updated);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task 제외된_직원의_이메일로_다시_올리면_복구되고_따로_집계된다()
    {
        var employee = Employee.Create("김철수", "charles@clovf.com", "01000000000", new DateOnly(2018, 3, 7), Today).Value;
        employee.MarkDeleted(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _repository.Seed(employee);

        const string Csv = "김철수, charles@clovf.com, 010-9999-8888, 2018.03.07";

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Created);
        Assert.Equal(0, result.Value.Updated);
        Assert.Equal(1, result.Value.Restored);
        Assert.False(employee.IsDeleted);
        Assert.Equal("010-9999-8888", employee.Tel.Formatted);
    }

    [Fact]
    public async Task 집계는_항상_전체_처리_건수와_일치한다()
    {
        _repository.Seed(Employee.Create("박영희", "matilda@clovf.com", "01087654321", new DateOnly(2021, 4, 28), Today).Value);

        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           박영희, matilda@clovf.com, 01087654321, 2021.04.28
                           """;

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsSuccess);
        var summary = result.Value;
        Assert.Equal(summary.TotalProcessed, summary.Created + summary.Updated + summary.Restored);
    }

    [Fact]
    public async Task 대소문자만_다른_이메일도_같은_직원으로_본다()
    {
        _repository.Seed(Employee.Create("김철수", "charles@clovf.com", "01000000000", new DateOnly(2018, 3, 7), Today).Value);

        const string Csv = "김철수, CHARLES@clovf.com, 010-9999-8888, 2018.03.07";

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Updated);
    }

    [Fact]
    public async Task 한_건이라도_유효하지_않으면_아무것도_저장하지_않는다()
    {
        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           박영희, 잘못된이메일, 01087654321, 2021.04.28
                           """;

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "employee.email_invalid");
        Assert.Contains("[2행]", result.FirstError.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Added);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task 여러_행의_실패를_한_번에_모아_반환한다()
    {
        const string Csv = """
                           , charles@clovf.com, 01075312468, 2018.03.07
                           박영희, 잘못된이메일, 01087654321, 2021.04.28
                           홍길동, kildong.hong@clovf.com, 전화번호아님, 2015.08.15
                           """;

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsFailure);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public async Task 같은_요청_안에서_이메일이_중복되면_실패한다()
    {
        const string Csv = """
                           김철수, charles@clovf.com, 01075312468, 2018.03.07
                           김철수2, charles@clovf.com, 01087654321, 2021.04.28
                           """;

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.email_duplicated_in_payload", result.FirstError.Code);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task 입사일_형식이_틀리면_허용_형식을_안내한다()
    {
        const string Csv = "김철수, charles@clovf.com, 01075312468, 07/03/2018";

        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload(Csv)), default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.joined_invalid", result.FirstError.Code);
        Assert.Contains("yyyy-MM-dd", result.FirstError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 본문이_비어_있으면_실패한다()
    {
        var result = await _handler.HandleAsync(new RegisterEmployeesCommand(new EmployeePayload("   ")), default);

        Assert.True(result.IsFailure);
        Assert.Equal("payload.empty", result.FirstError.Code);
    }
}
