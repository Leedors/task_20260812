using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Domain.Employees;

/// <summary>
/// 직원 연락처. 이 애그리게이트가 "긴급 연락망"의 단위다.
/// </summary>
/// <remarks>
/// 세터를 모두 private 으로 닫고 <see cref="Create"/> / <see cref="UpdateContact"/> 만 열어둔 이유는,
/// 유효하지 않은 상태의 Employee 가 애초에 만들어질 수 없게 하기 위해서다.
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

        if (errors.Count > 0)
        {
            return Result.Failure<Employee>(errors);
        }

        return Result.Success(new Employee(trimmedName!, emailResult.Value, telResult.Value, joined!.Value));
    }

    /// <summary>
    /// 같은 이메일로 다시 업로드된 경우 연락처를 최신 값으로 갱신한다.
    /// (이메일은 식별자이므로 변경 대상이 아니다.)
    /// </summary>
    public void UpdateContact(string name, PhoneNumber tel, DateOnly joined)
    {
        Name = name;
        Tel = tel;
        Joined = joined;
    }
}
