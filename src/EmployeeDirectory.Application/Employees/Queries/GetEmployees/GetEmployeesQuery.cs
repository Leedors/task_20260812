using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Employees.Queries.GetEmployees;

/// <summary>직원 목록을 페이지 단위로 조회한다. 검색어가 있으면 부분 일치로 좁힌다.</summary>
public sealed record GetEmployeesQuery(int Page, int PageSize, string? Keyword = null)
    : IQuery<PagedResult<EmployeeDto>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;

    /// <summary>한 번의 호출로 DB/네트워크를 과도하게 점유하지 못하도록 상한을 둔다.</summary>
    public const int MaxPageSize = 200;

    public const int MaxKeywordLength = 100;
}

internal sealed class GetEmployeesQueryValidator : IValidator<GetEmployeesQuery>
{
    public IReadOnlyList<Error> Validate(GetEmployeesQuery instance)
    {
        var errors = new List<Error>();

        if (instance.Page < 1)
        {
            errors.Add(Error.Validation("paging.page_invalid", "page 는 1 이상이어야 합니다."));
        }

        if (instance.PageSize < 1)
        {
            errors.Add(Error.Validation("paging.page_size_invalid", "pageSize 는 1 이상이어야 합니다."));
        }
        else if (instance.PageSize > GetEmployeesQuery.MaxPageSize)
        {
            errors.Add(Error.Validation(
                "paging.page_size_too_large",
                $"pageSize 는 최대 {GetEmployeesQuery.MaxPageSize} 까지 허용됩니다."));
        }

        if (instance.Keyword is { Length: > GetEmployeesQuery.MaxKeywordLength })
        {
            errors.Add(Error.Validation(
                "search.keyword_too_long",
                $"검색어는 최대 {GetEmployeesQuery.MaxKeywordLength}자까지 허용됩니다."));
        }

        return errors;
    }
}

internal sealed class GetEmployeesQueryHandler(IEmployeeReadStore readStore)
    : IQueryHandler<GetEmployeesQuery, PagedResult<EmployeeDto>>
{
    public async Task<Result<PagedResult<EmployeeDto>>> HandleAsync(
        GetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        var page = await readStore
            .GetPageAsync(query.Page, query.PageSize, query.Keyword?.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(page);
    }
}
