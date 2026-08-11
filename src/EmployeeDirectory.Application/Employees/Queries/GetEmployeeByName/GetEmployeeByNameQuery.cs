using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Employees.Queries.GetEmployeeByName;

/// <summary>이름으로 직원 한 명의 상세 연락처를 조회한다.</summary>
public sealed record GetEmployeeByNameQuery(string Name) : IQuery<EmployeeDto>;

internal sealed class GetEmployeeByNameQueryValidator : IValidator<GetEmployeeByNameQuery>
{
    public IReadOnlyList<Error> Validate(GetEmployeeByNameQuery instance)
        => string.IsNullOrWhiteSpace(instance.Name)
            ? new[] { Error.Validation("employee.name_required", "이름은 필수입니다.") }
            : Array.Empty<Error>();
}

internal sealed class GetEmployeeByNameQueryHandler(IEmployeeReadStore readStore)
    : IQueryHandler<GetEmployeeByNameQuery, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> HandleAsync(
        GetEmployeeByNameQuery query,
        CancellationToken cancellationToken)
    {
        var name = query.Name.Trim();

        var employee = await readStore.FindByNameAsync(name, cancellationToken).ConfigureAwait(false);

        return employee is null
            ? Result.Failure<EmployeeDto>(
                Error.NotFound("employee.not_found", $"'{name}' 이름의 직원을 찾을 수 없습니다."))
            : Result.Success(employee);
    }
}
