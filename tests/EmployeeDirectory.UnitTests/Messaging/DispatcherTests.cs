using EmployeeDirectory.Application;
using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Application.Employees.Queries.GetEmployees;
using EmployeeDirectory.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployeeDirectory.UnitTests.Messaging;

/// <summary>
/// 직접 구현한 CQRS 디스패처가 핸들러를 찾아 파이프라인과 함께 실행하는지 검증한다.
/// </summary>
public sealed class DispatcherTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IEmployeeReadStore>(new FakeEmployeeReadStore());
        services.AddApplication();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task 쿼리를_해당_핸들러로_전달한다()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(
            new GetEmployeesQuery(1, 10),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task 검증_behavior가_핸들러보다_먼저_동작한다()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IQueryDispatcher>();

        // pageSize 0 은 핸들러에 도달하기 전에 ValidationBehavior 가 걸러야 한다.
        var result = await dispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(
            new GetEmployeesQuery(1, 0),
            default);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("paging.page_size_invalid");
    }

    [Fact]
    public async Task 같은_쿼리를_반복_호출해도_동작이_동일하다()
    {
        await using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IQueryDispatcher>();

        var first = await dispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(
            new GetEmployeesQuery(1, 5),
            default);
        var second = await dispatcher.QueryAsync<GetEmployeesQuery, PagedResult<EmployeeDto>>(
            new GetEmployeesQuery(2, 5),
            default);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Page.Should().Be(2);
    }
}
