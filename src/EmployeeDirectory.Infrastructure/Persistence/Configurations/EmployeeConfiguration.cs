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
/// 조회 조건에서 값 객체를 쓸 수 없기 때문이다(대량 upsert 시 이메일 IN 절이 필요하다).
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
            email.HasIndex(value => value.Value).IsUnique();
        });

        builder.OwnsOne(employee => employee.Tel, tel =>
        {
            tel.Property(value => value.Value)
                .HasColumnName("tel")
                .HasMaxLength(20)
                .IsRequired();

            // 표시용 파생 값은 저장하지 않는다.
            tel.Ignore(value => value.Formatted);
        });

        builder.Navigation(employee => employee.Email).IsRequired();
        builder.Navigation(employee => employee.Tel).IsRequired();

        builder.Property(employee => employee.Joined)
            .HasColumnName("joined")
            .IsRequired();

        // 이름 조회가 주 사용 패턴이므로 인덱스를 둔다(유일 인덱스가 아님 — 동명이인 허용).
        builder.HasIndex(employee => employee.Name);
    }
}
