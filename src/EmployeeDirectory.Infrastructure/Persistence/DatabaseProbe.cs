using EmployeeDirectory.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Persistence;

internal sealed class DatabaseProbe(ApplicationDbContext dbContext) : IDatabaseProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        => dbContext.Database.CanConnectAsync(cancellationToken);
}
