using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EmployeeDirectory.Infrastructure.Persistence;

internal sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTimeProvider dateTimeProvider) : DbContext(options), IUnitOfWork
{
    public DbSet<Employee> Employees => Set<Employee>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// 생성·수정 시각을 저장 직전에 일괄로 찍는다.
    /// </summary>
    /// <remarks>
    /// 도메인 메서드마다 시간을 넘기는 대신 여기서 처리하는 이유는, 감사 필드가
    /// "모든 변경에 기계적으로 적용되는" 관심사라 <b>빠뜨릴 여지를 없애는 것</b>이 중요하기 때문이다.
    /// <para>
    /// 주의: 값 객체를 Owned Type 으로 매핑했기 때문에 전화번호만 바뀐 경우
    /// 소유자 엔트리는 <c>Unchanged</c> 로 남고 owned 엔트리만 <c>Modified</c> 가 된다.
    /// 소유자 상태만 보면 이 변경을 놓치므로 참조(owned) 엔트리까지 함께 확인한다.
    /// </para>
    /// </remarks>
    private void StampAuditFields()
    {
        var now = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Employee>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(employee => employee.CreatedAt).CurrentValue = now;
                entry.Property(employee => employee.UpdatedAt).CurrentValue = now;
                continue;
            }

            if (entry.State == EntityState.Modified || HasModifiedOwnedValue(entry))
            {
                entry.Property(employee => employee.UpdatedAt).CurrentValue = now;
            }
        }
    }

    private static bool HasModifiedOwnedValue(EntityEntry<Employee> entry)
        => entry.References.Any(reference =>
            reference.TargetEntry?.State is EntityState.Modified or EntityState.Added);
}
