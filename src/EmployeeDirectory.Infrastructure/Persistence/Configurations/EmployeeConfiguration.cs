using EmployeeDirectory.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeDirectory.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Employee"/> 매핑.
/// </summary>
/// <remarks>
/// 값 객체는 <c>ValueConverter</c> 가 아니라 <b>Owned Type</b> 으로 매핑했다.
/// 컨버터로 매핑하면 <c>e.Email.Value</c> 같은 표현이 SQL 로 번역되지 않아
/// 조회 조건에서 값 객체를 쓸 수 없기 때문이다(대량 upsert 의 IN 절, 검색의 LIKE 절이 모두 필요하다).
/// Owned Type 은 소유자 테이블의 컬럼으로 펼쳐지므로 스키마는 단순한 단일 테이블 그대로다.
/// </remarks>
internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(employee => employee.Name)
            .HasColumnName("name")
            .HasMaxLength(Employee.MaxNameLength)
            .IsRequired();

        builder.OwnsOne(employee => employee.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("email")
                .HasMaxLength(EmailAddress.MaxLength)
                .IsRequired();

            // 이메일을 자연 키로 쓰므로 DB 레벨에서도 유일성을 보장한다.
            // soft delete 된 행도 이 인덱스를 계속 점유하므로, 같은 이메일 재업로드는
            // "새로 추가"가 아니라 "복구 + 갱신"으로 처리된다(RegisterEmployeesCommandHandler 참고).
            email.HasIndex(value => value.Value).IsUnique();
        });

        builder.OwnsOne(employee => employee.Tel, tel =>
        {
            tel.Property(value => value.Value)
                .HasColumnName("tel")
                .HasMaxLength(20)
                .IsRequired();

            // Formatted / Masked 같은 계산 프로퍼티는 setter 도 backing field 도 없어서
            // EF 가 매핑 대상으로 삼지 않는다. Ignore() 를 따로 호출할 필요가 없다.
            // (생성된 employees 테이블에 해당 컬럼이 없는 것으로 확인)
        });

        builder.Navigation(employee => employee.Email).IsRequired();
        builder.Navigation(employee => employee.Tel).IsRequired();

        builder.Property(employee => employee.Joined)
            .HasColumnName("joined")
            .IsRequired();

        builder.Property(employee => employee.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(employee => employee.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(employee => employee.DeletedAt)
            .HasColumnName("deleted_at");

        // 이름 조회/검색이 주 사용 패턴이므로 인덱스를 둔다(유일 인덱스가 아님 — 동명이인 허용).
        builder.HasIndex(employee => employee.Name);

        // 제외된 직원을 매번 조건에 적는 것은 빠뜨리기 쉬우므로 모델 레벨에서 걸러낸다.
        // 복구·유일성 확인처럼 삭제된 행까지 봐야 하는 경우에만 IgnoreQueryFilters() 로 명시적으로 푼다.
        builder.HasQueryFilter(employee => employee.DeletedAt == null);
    }
}
