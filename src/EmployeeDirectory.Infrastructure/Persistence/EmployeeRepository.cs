using EmployeeDirectory.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Persistence;

internal sealed class EmployeeRepository(ApplicationDbContext dbContext) : IEmployeeRepository
{
    public async Task<IReadOnlyDictionary<string, Employee>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        }

        // 업로드 1건당 쿼리 1회. 행마다 조회하면 N+1 이 되므로 IN 절로 한 번에 가져온다.
        var normalized = emails.Select(email => email.ToLowerInvariant()).Distinct().ToArray();

        var found = await dbContext.Employees
            .IgnoreQueryFilters() // 제외된 직원도 찾아야 재업로드 시 복구할 수 있다.
            .Where(employee => normalized.Contains(employee.Email.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(employee => employee.Email.Value, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Employee?> FindByIdAsync(int id, CancellationToken cancellationToken)
        => dbContext.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, int excludedId, CancellationToken cancellationToken)
        => dbContext.Employees
            .IgnoreQueryFilters() // 제외된 직원도 유일 인덱스를 점유하므로 충돌 대상이다.
            .AnyAsync(
                employee => employee.Email.Value == email && employee.Id != excludedId,
                cancellationToken);

    public void AddRange(IEnumerable<Employee> employees) => dbContext.Employees.AddRange(employees);
}
