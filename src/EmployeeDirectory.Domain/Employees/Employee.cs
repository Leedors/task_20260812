using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Domain.Employees;

/// <summary>
/// 직원 연락처. 이 애그리게이트가 "긴급 연락망"의 단위다.
/// </summary>
/// <remarks>
/// 세터를 모두 private 으로 닫고 <see cref="Create"/> / <see cref="Replace"/> 등 의도가 드러나는
/// 메서드만 열어둔 이유는, 유효하지 않은 상태의 Employee 가 애초에 만들어질 수 없게 하기 위해서다.
/// </remarks>
public sealed class Employee
{
    public const int MaxNameLength = 100;

    // EF Core 머티리얼라이제이션 전용
    private Employee()
    {
        Name = null!;
        Email = null!;
        Tel = null!;
    }

    private Employee(string name, EmailAddress email, PhoneNumber tel, DateOnly joined)
    {
        Name = name;
        Email = email;
        Tel = tel;
        Joined = joined;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>이메일은 직원을 식별하는 자연 키다(업로드 시 중복 판정 기준).</summary>
    public EmailAddress Email { get; private set; }

    public PhoneNumber Tel { get; private set; }

    public DateOnly Joined { get; private set; }

    /// <remarks>
    /// 생성/수정 시각은 모든 엔티티에 동일하게 적용되는 기술적 관심사라
    /// 도메인 메서드마다 시간을 넘기지 않고 저장 시점(<c>SaveChanges</c>)에 일괄로 찍는다.
    /// 이렇게 하면 새 메서드를 추가할 때 갱신을 빠뜨릴 여지가 없다.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <inheritdoc cref="CreatedAt"/>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// 삭제(퇴사·제외) 시각. 값이 있으면 조회 대상에서 제외된다.
    /// </summary>
    /// <remarks>
    /// 물리 삭제를 하지 않는 이유: 연락망에서 사람이 빠진 것은 "기록해야 할 사건"이고,
    /// 오삭제 복구와 감사 추적이 가능해야 하기 때문이다.
    /// 생성/수정 시각과 달리 <b>도메인 행위의 결과</b>이므로 시각을 명시적으로 받는다.
    /// </remarks>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>검증을 통과한 경우에만 인스턴스를 만든다. 실패 사유는 <b>모아서</b> 반환한다.</summary>
    /// <param name="name">이름.</param>
    /// <param name="email">이메일.</param>
    /// <param name="tel">전화번호.</param>
    /// <param name="joined">입사일.</param>
    /// <param name="today">
    /// 입사일이 미래인지 판정하기 위한 기준일. 도메인이 <c>DateTime.Now</c> 를 직접 읽지 않게 해
    /// 테스트에서 시간을 고정할 수 있도록 주입받는다.
    /// </param>
    public static Result<Employee> Create(
        string? name,
        string? email,
        string? tel,
        DateOnly? joined,
        DateOnly today)
    {
        var validated = Validate(name, email, tel, joined, today);

        return validated.IsFailure
            ? Result.Failure<Employee>(validated.Errors)
            : Result.Success(new Employee(
                validated.Value.Name,
                validated.Value.Email,
                validated.Value.Tel,
                validated.Value.Joined));
    }

    /// <summary>
    /// 모든 필드를 교체한다(PUT 시맨틱). 이메일까지 바뀔 수 있다.
    /// </summary>
    /// <remarks>
    /// 이메일이 자연 키이므로 <b>다른 직원과 충돌하지 않는지</b>는 애그리게이트 밖(핸들러)에서 확인한다.
    /// 애그리게이트는 자기 자신의 불변식만 책임진다.
    /// </remarks>
    public Result Replace(string? name, string? email, string? tel, DateOnly? joined, DateOnly today)
    {
        var validated = Validate(name, email, tel, joined, today);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Errors);
        }

        Name = validated.Value.Name;
        Email = validated.Value.Email;
        Tel = validated.Value.Tel;
        Joined = validated.Value.Joined;

        return Result.Success();
    }

    /// <summary>
    /// 같은 이메일로 다시 업로드된 경우 연락처를 최신 값으로 갱신한다.
    /// (이메일은 식별자이므로 이 경로에서는 변경 대상이 아니다.)
    /// </summary>
    public void UpdateContact(string name, PhoneNumber tel, DateOnly joined)
    {
        Name = name;
        Tel = tel;
        Joined = joined;
    }

    /// <summary>삭제 상태를 해제한다(재입사 또는 오삭제 복구).</summary>
    public void Restore() => DeletedAt = null;

    public void MarkDeleted(DateTimeOffset deletedAt) => DeletedAt = deletedAt;

    private static Result<ValidatedFields> Validate(
        string? name,
        string? email,
        string? tel,
        DateOnly? joined,
        DateOnly today)
    {
        var errors = new List<Error>();

        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            errors.Add(Error.Validation("employee.name_required", "이름은 필수입니다."));
        }
        else if (trimmedName.Length > MaxNameLength)
        {
            errors.Add(Error.Validation("employee.name_too_long", $"이름은 {MaxNameLength}자를 넘을 수 없습니다."));
        }

        var emailResult = EmailAddress.Create(email);
        if (emailResult.IsFailure)
        {
            errors.AddRange(emailResult.Errors);
        }

        var telResult = PhoneNumber.Create(tel);
        if (telResult.IsFailure)
        {
            errors.AddRange(telResult.Errors);
        }

        if (joined is null)
        {
            errors.Add(Error.Validation("employee.joined_required", "입사일은 필수입니다."));
        }
        else if (joined.Value > today)
        {
            errors.Add(Error.Validation("employee.joined_in_future", $"입사일이 미래일 수 없습니다: {joined.Value:yyyy-MM-dd}"));
        }

        return errors.Count > 0
            ? Result.Failure<ValidatedFields>(errors)
            : Result.Success(new ValidatedFields(trimmedName!, emailResult.Value, telResult.Value, joined!.Value));
    }

    private sealed record ValidatedFields(string Name, EmailAddress Email, PhoneNumber Tel, DateOnly Joined);
}
