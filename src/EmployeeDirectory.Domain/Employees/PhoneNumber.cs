using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Domain.Employees;

/// <summary>
/// 전화번호 값 객체.
/// </summary>
/// <remarks>
/// 과제의 두 입력 예시가 서로 다른 표기를 쓴다.
/// <list type="bullet">
///   <item>csv: <c>01075312468</c> (구분자 없음)</item>
///   <item>json: <c>010-1111-2424</c> (하이픈)</item>
/// </list>
/// 저장은 숫자만 남긴 정규화 형태로 하고, 출력은 하이픈 표기로 통일한다.
/// 이렇게 하면 표기가 달라도 같은 번호를 같은 값으로 비교할 수 있다.
/// </remarks>
public sealed record PhoneNumber
{
    private const int MinDigits = 9;
    private const int MaxDigits = 11;

    private PhoneNumber(string value) => Value = value;

    /// <summary>숫자만 남긴 정규화 값 (예: <c>01075312468</c>).</summary>
    public string Value { get; }

    /// <summary>하이픈이 포함된 표시용 값 (예: <c>010-7531-2468</c>).</summary>
    public string Formatted => Format(Value);

    /// <summary>로그·외부 노출용 마스킹 값 (예: <c>010-****-2468</c>).</summary>
    /// <remarks>가운데 국번만 가린다. 뒤 네 자리는 남겨야 장애 추적에서 사람을 구분할 수 있다.</remarks>
    public string Masked
    {
        get
        {
            var parts = Formatted.Split('-');

            return parts.Length == 3
                ? $"{parts[0]}-{new string('*', parts[1].Length)}-{parts[2]}"
                : new string('*', Formatted.Length);
        }
    }

    public static Result<PhoneNumber> Create(string? input)
    {
        var trimmed = input?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("employee.tel_required", "전화번호는 필수입니다."));
        }

        var digits = Normalize(trimmed);

        if (digits.Length is < MinDigits or > MaxDigits || !digits.StartsWith('0'))
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("employee.tel_invalid", $"전화번호 형식이 올바르지 않습니다: '{trimmed}'"));
        }

        return Result.Success(new PhoneNumber(digits));
    }

    /// <summary>
    /// 이미 검증되어 저장된 값을 다시 객체로 복원할 때만 사용한다(ORM 매핑 전용).
    /// </summary>
    public static PhoneNumber FromStorage(string value) => new(value);

    public override string ToString() => Formatted;

    /// <summary>
    /// 검색어처럼 "번호가 아닐 수도 있는" 입력에서 숫자만 뽑는다.
    /// 저장 값이 숫자열이므로, 사용자가 <c>010-1234</c> 로 검색해도 매칭되게 하려면 이 정규화가 필요하다.
    /// </summary>
    public static string DigitsOf(string? input)
        => string.IsNullOrEmpty(input) ? string.Empty : new string(input.Where(char.IsAsciiDigit).ToArray());

    /// <summary>국가번호(+82)와 구분자를 제거해 국내 표기 숫자열로 만든다.</summary>
    private static string Normalize(string input)
    {
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        // +82-10-1234-5678 / 82 10 1234 5678 → 01012345678
        if (input.StartsWith('+') && digits.StartsWith("82", StringComparison.Ordinal))
        {
            digits = "0" + digits[2..];
        }

        return digits;
    }

    private static string Format(string digits) => digits switch
    {
        // 서울 지역번호(02)는 국번이 3자리 또는 4자리다.
        { Length: 9 } when digits.StartsWith("02", StringComparison.Ordinal) => $"02-{digits[2..5]}-{digits[5..]}",
        { Length: 10 } when digits.StartsWith("02", StringComparison.Ordinal) => $"02-{digits[2..6]}-{digits[6..]}",
        { Length: 11 } => $"{digits[..3]}-{digits[3..7]}-{digits[7..]}",
        { Length: 10 } => $"{digits[..3]}-{digits[3..6]}-{digits[6..]}",
        _ => digits
    };
}
