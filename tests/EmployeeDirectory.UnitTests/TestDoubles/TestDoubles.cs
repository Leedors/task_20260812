using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Employees;

namespace EmployeeDirectory.UnitTests.TestDoubles;

/// <summary>
/// 메모리 기반 저장소 대역.
/// </summary>
/// <remarks>
/// 모킹 라이브러리를 쓰지 않고 손으로 만든 이유: 대역이 3개뿐이고 동작이 단순해
/// 셋업 코드보다 구현이 짧으며, 테스트가 "무엇을 검증하는지" 더 잘 드러나기 때문이다.
/// </remarks>
internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly Dictionary<string, Employee> _existing = new(StringComparer.OrdinalIgnoreCase);

    public List<Employee> Added { get; } = [];

    public void Seed(Employee employee) => _existing[employee.Email.Value] = employee;

    public Task<IReadOnlyDictionary<string, Employee>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken)
    {
        var matched = _existing
            .Where(pair => emails.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, Employee>>(matched);
    }

    public void AddRange(IEnumerable<Employee> employees) => Added.AddRange(employees);
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
}

internal sealed class FakeEmployeeReadStore : IEmployeeReadStore
{
    public List<EmployeeDto> Employees { get; } = [];

    public Task<PagedResult<EmployeeDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = Employees.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new PagedResult<EmployeeDto>(items, page, pageSize, Employees.Count));
    }

    public Task<EmployeeDto?> FindByNameAsync(string name, CancellationToken cancellationToken)
        => Task.FromResult(Employees.FirstOrDefault(employee => employee.Name == name));
}
