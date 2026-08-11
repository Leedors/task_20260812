using System.Text.RegularExpressions;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Domain.Employees;

/// <summary>
/// 이메일 주소 값 객체.
/// </summary>
/// <remarks>
/// 원시 <c>string</c> 대신 값 객체를 쓰는 이유는 "검증되지 않은 이메일"이라는 상태가
/// 도메인 안으로 들어올 수 없게 만들기 위해서다. 생성자는 private 이고 <see cref="Create"/> 만이 입구다.
/// </remarks>
public sealed partial record EmailAddress
{
    /// <summary>RFC 5321 상 실제 주소 최대 길이.</summary>
    public const int MaxLength = 254;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// 로그·외부 노출용 마스킹 값 (예: <c>ch***@example.com</c>).
    /// </summary>
    /// <remarks>
    /// 로그는 원본 값을 남기기 가장 쉬운 곳이면서, 보존 기간이 길고 접근 통제가 느슨한 곳이다.
    /// 장애 추적에는 "누구인지 구분되는 정도"면 충분하므로 계정부 앞 두 글자만 남긴다.
    /// </remarks>
    public string Masked
    {
        get
        {
            var separator = Value.IndexOf('@', StringComparison.Ordinal);
            if (separator <= 0)
            {
                return "***";
            }

            var local = Value[..separator];
            var domain = Value[separator..];

            return local.Length <= 2
                ? $"{local[0]}***{domain}"
                : $"{local[..2]}***{domain}";
        }
    }

    public static Result<EmailAddress> Create(string? input)
    {
        var trimmed = input?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<EmailAddress>(
                Error.Validation("employee.email_required", "이메일은 필수입니다."));
        }

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<EmailAddress>(
                Error.Validation("employee.email_too_long", $"이메일은 {MaxLength}자를 넘을 수 없습니다."));
        }

        if (!EmailPattern().IsMatch(trimmed))
        {
            return Result.Failure<EmailAddress>(
                Error.Validation("employee.email_invalid", $"이메일 형식이 올바르지 않습니다: '{trimmed}'"));
        }

        // 도메인부는 대소문자를 구분하지 않으므로 소문자로 정규화해 중복 판정을 안정화한다.
        return Result.Success(new EmailAddress(trimmed.ToLowerInvariant()));
    }

    /// <summary>
    /// 이미 검증되어 저장된 값을 다시 객체로 복원할 때만 사용한다(ORM 매핑 전용).
    /// </summary>
    public static EmailAddress FromStorage(string value) => new(value);

    public override string ToString() => Value;

    // 과제 목적상 "실용적인" 수준의 검증. 완전한 RFC 5322 파싱은 오히려 오탐이 많아 채택하지 않았다.
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex EmailPattern();
}
