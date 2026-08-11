using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Application.Employees.Queries.GetEmployeeByName;
using EmployeeDirectory.Application.Employees.Queries.GetEmployees;
using EmployeeDirectory.UnitTests.TestDoubles;

namespace EmployeeDirectory.UnitTests.Employees;

public sealed class GetEmployeesQueryTests
{
    private readonly FakeEmployeeReadStore _readStore = new();

    public GetEmployeesQueryTests()
    {
        for (var i = 1; i <= 25; i++)
        {
            _readStore.Employees.Add(new EmployeeDto(i, $"직원{i:00}", $"user{i:00}@clovf.com", "010-0000-0000", new DateOnly(2020, 1, 1)));
        }
    }

    [Fact]
    public async Task 요청한_페이지의_항목과_전체_건수를_함께_반환한다()
    {
        var handler = new GetEmployeesQueryHandler(_readStore);

        var result = await handler.HandleAsync(new GetEmployeesQuery(2, 10), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Items.Count);
        Assert.Equal("직원11", result.Value.Items[0].Name);
        Assert.Equal(25, result.Value.TotalCount);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.True(result.Value.HasPreviousPage);
        Assert.True(result.Value.HasNextPage);
    }

    [Fact]
    public async Task 마지막_페이지는_다음_페이지가_없다()
    {
        var handler = new GetEmployeesQueryHandler(_readStore);

        var result = await handler.HandleAsync(new GetEmployeesQuery(3, 10), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.Items.Count);
        Assert.False(result.Value.HasNextPage);
    }

    [Theory]
    [InlineData(0, 10, "paging.page_invalid")]
    [InlineData(-1, 10, "paging.page_invalid")]
    [InlineData(1, 0, "paging.page_size_invalid")]
    [InlineData(1, 100_000, "paging.page_size_too_large")]
    public void 페이징_파라미터가_범위를_벗어나면_검증에_실패한다(int page, int pageSize, string expectedCode)
    {
        var validator = new GetEmployeesQueryValidator();

        var errors = validator.Validate(new GetEmployeesQuery(page, pageSize));

        Assert.Contains(errors, error => error.Code == expectedCode);
    }

    [Fact]
    public void 정상_파라미터는_검증을_통과한다()
    {
        var validator = new GetEmployeesQueryValidator();

        Assert.Empty(validator.Validate(new GetEmployeesQuery(1, 20)));
    }
}

public sealed class GetEmployeeByNameQueryTests
{
    private readonly FakeEmployeeReadStore _readStore = new();

    public GetEmployeeByNameQueryTests()
        => _readStore.Employees.Add(new EmployeeDto(1, "김철수", "charles@clovf.com", "010-7531-2468", new DateOnly(2018, 3, 7)));

    [Fact]
    public async Task 이름이_일치하면_상세를_반환한다()
    {
        var handler = new GetEmployeeByNameQueryHandler(_readStore);

        var result = await handler.HandleAsync(new GetEmployeeByNameQuery("김철수"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("charles@clovf.com", result.Value.Email);
    }

    [Fact]
    public async Task 앞뒤_공백은_무시하고_조회한다()
    {
        var handler = new GetEmployeeByNameQueryHandler(_readStore);

        var result = await handler.HandleAsync(new GetEmployeeByNameQuery("  김철수 "), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task 일치하는_직원이_없으면_NotFound를_반환한다()
    {
        var handler = new GetEmployeeByNameQueryHandler(_readStore);

        var result = await handler.HandleAsync(new GetEmployeeByNameQuery("없는사람"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("employee.not_found", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void 이름이_비어_있으면_검증에_실패한다(string name)
    {
        var validator = new GetEmployeeByNameQueryValidator();

        var error = Assert.Single(validator.Validate(new GetEmployeeByNameQuery(name)));
        Assert.Equal("employee.name_required", error.Code);
    }
}
