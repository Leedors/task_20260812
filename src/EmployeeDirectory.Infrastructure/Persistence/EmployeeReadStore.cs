using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>
/// 조회 전용 구현. 애그리게이트를 거치지 않고 필요한 컬럼만 투영한다.
/// </summary>
/// <remarks>
/// 삭제(제외)된 직원은 모델의 전역 쿼리 필터가 걸러내므로 여기서 따로 조건을 적지 않는다.
/// </remarks>
internal sealed class EmployeeReadStore(ApplicationDbContext dbContext) : IEmployeeReadStore
{
    /// <summary>LIKE 패턴에서 <c>%</c>, <c>_</c> 를 리터럴로 다루기 위한 이스케이프 문자.</summary>
    private const string LikeEscapeCharacter = "\\";

    public async Task<PagedResult<EmployeeDto>> GetPageAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = ApplyKeyword(dbContext.Employees.AsNoTracking(), keyword);

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
                employee.Joined,
                employee.CreatedAt,
                employee.UpdatedAt))
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
                employee.Joined,
                employee.CreatedAt,
                employee.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToDto(row);
    }

    /// <summary>
    /// 이름·이메일·전화번호 부분 일치 검색.
    /// </summary>
    /// <remarks>
    /// <para><c>string.Contains</c> 대신 <c>EF.Functions.Like</c> 를 쓴 이유: SQLite 에서 전자는
    /// 대소문자를 구분하는 <c>instr()</c> 로 번역되지만, <c>LIKE</c> 는 ASCII 범위에서 대소문자를
    /// 구분하지 않는다. 이메일 검색은 대소문자 구분이 없어야 자연스럽다.</para>
    /// <para>사용자가 입력한 <c>%</c>, <c>_</c> 는 와일드카드가 아니라 글자로 취급해야 하므로 이스케이프한다.
    /// 이걸 빠뜨리면 <c>%</c> 한 글자를 검색했을 때 전체 행이 나온다.</para>
    /// <para>전화번호는 저장 값이 숫자열이므로 검색어에서도 숫자만 뽑아 비교한다.
    /// 덕분에 <c>010-7531</c> 로 검색해도 <c>01075312468</c> 이 매칭된다.</para>
    /// </remarks>
    private static IQueryable<Employee> ApplyKeyword(IQueryable<Employee> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var pattern = $"%{EscapeLike(keyword)}%";
        var digits = PhoneNumber.DigitsOf(keyword);

        if (digits.Length == 0)
        {
            return query.Where(employee =>
                EF.Functions.Like(employee.Name, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(employee.Email.Value, pattern, LikeEscapeCharacter));
        }

        var telPattern = $"%{digits}%";

        return query.Where(employee =>
            EF.Functions.Like(employee.Name, pattern, LikeEscapeCharacter) ||
            EF.Functions.Like(employee.Email.Value, pattern, LikeEscapeCharacter) ||
            EF.Functions.Like(employee.Tel.Value, telPattern, LikeEscapeCharacter));
    }

    private static string EscapeLike(string input) => input
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>전화번호는 저장된 정규화 값을 표시용 형식으로 바꿔서 내려준다.</summary>
    private static EmployeeDto ToDto(Row row)
        => new(
            row.Id,
            row.Name,
            row.Email,
            PhoneNumber.FromStorage(row.Tel).Formatted,
            row.Joined,
            row.CreatedAt,
            row.UpdatedAt);

    private sealed record Row(
        int Id,
        string Name,
        string Email,
        string Tel,
        DateOnly Joined,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
