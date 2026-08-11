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
            .Where(employee => normalized.Contains(employee.Email.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToDictionary(employee => employee.Email.Value, StringComparer.OrdinalIgnoreCase);
    }

    public void AddRange(IEnumerable<Employee> employees) => dbContext.Employees.AddRange(employees);
}
