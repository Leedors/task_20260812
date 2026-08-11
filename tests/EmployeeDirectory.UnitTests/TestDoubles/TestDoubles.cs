using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.TestDoubles;

/// <summary>
/// 메모리 기반 저장소 대역.
/// </summary>
/// <remarks>
/// 모킹 라이브러리를 쓰지 않고 손으로 만든 이유: 대역이 몇 개뿐이고 동작이 단순해
/// 셋업 코드보다 구현이 짧으며, 테스트가 "무엇을 검증하는지" 더 잘 드러나기 때문이다.
/// </remarks>
internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly List<Entry> _entries = [];
    private int _nextId = 1;

    public List<Employee> Added { get; } = [];

    /// <summary>기존 직원을 심고 식별자를 돌려준다(실제 DB 의 자동 증가 키를 흉내낸다).</summary>
    public int Seed(Employee employee)
    {
        var id = _nextId++;
        _entries.Add(new Entry(id, employee));
        return id;
    }

    public Task<IReadOnlyDictionary<string, Employee>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken)
    {
        // 실제 구현과 동일하게 삭제된 직원도 포함한다.
        var matched = _entries
            .Where(entry => emails.Contains(entry.Employee.Email.Value, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Employee.Email.Value, entry => entry.Employee, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, Employee>>(matched);
    }

    public Task<Employee?> FindByIdAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(_entries
            .FirstOrDefault(entry => entry.Id == id && !entry.Employee.IsDeleted)?.Employee);

    public Task<bool> ExistsByEmailAsync(string email, int excludedId, CancellationToken cancellationToken)
        => Task.FromResult(_entries.Any(entry =>
            entry.Id != excludedId &&
            string.Equals(entry.Employee.Email.Value, email, StringComparison.OrdinalIgnoreCase)));

    public void AddRange(IEnumerable<Employee> employees) => Added.AddRange(employees);

    private sealed record Entry(int Id, Employee Employee);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

internal sealed class FixedDateTimeProvider(DateOnly today) : IDateTimeProvider
{
    public DateOnly Today { get; } = today;

    public DateTimeOffset UtcNow { get; } = new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

internal sealed class FakeEmployeeReadStore : IEmployeeReadStore
{
    public List<EmployeeDto> Employees { get; } = [];

    public Task<PagedResult<EmployeeDto>> GetPageAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var source = string.IsNullOrWhiteSpace(keyword)
            ? Employees
            : Employees
                .Where(employee =>
                    employee.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    employee.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<EmployeeDto>(items, page, pageSize, source.Count));
    }

    public Task<EmployeeDto?> FindByNameAsync(string name, CancellationToken cancellationToken)
        => Task.FromResult(Employees.FirstOrDefault(employee => employee.Name == name));
}
