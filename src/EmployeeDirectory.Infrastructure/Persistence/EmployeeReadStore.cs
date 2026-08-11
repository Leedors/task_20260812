using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>
/// 조회 전용 구현. 애그리게이트를 거치지 않고 필요한 컬럼만 투영한다.
/// </summary>
internal sealed class EmployeeReadStore(ApplicationDbContext dbContext) : IEmployeeReadStore
{
    public async Task<PagedResult<EmployeeDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Employees.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount == 0)
        {
            return PagedResult<EmployeeDto>.Empty(page, pageSize);
        }

        var rows = await query
            .OrderBy(employee => employee.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(employee => new Row(
                employee.Id,
                employee.Name,
                employee.Email.Value,
                employee.Tel.Value,
                employee.Joined))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<EmployeeDto>(rows.Select(ToDto).ToArray(), page, pageSize, totalCount);
    }

    public async Task<EmployeeDto?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        var row = await dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.Name == name)
            .OrderBy(employee => employee.Id)
            .Select(employee => new Row(
                employee.Id,
                employee.Name,
                employee.Email.Value,
                employee.Tel.Value,
                employee.Joined))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDto(row);
    }

    /// <summary>전화번호는 저장된 정규화 값을 표시용 형식으로 바꿔서 내려준다.</summary>
    private static EmployeeDto ToDto(Row row)
        => new(row.Id, row.Name, row.Email, PhoneNumber.FromStorage(row.Tel).Formatted, row.Joined);

    private sealed record Row(int Id, string Name, string Email, string Tel, DateOnly Joined);
}
