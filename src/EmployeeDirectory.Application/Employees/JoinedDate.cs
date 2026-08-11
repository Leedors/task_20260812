using System.Globalization;

namespace EmployeeDirectory.Application.Employees;

/// <summary>
/// 입사일 문자열 파싱.
/// </summary>
/// <remarks>
/// 과제의 두 예시가 서로 다른 표기를 쓴다: csv 는 <c>2018.03.07</c>, json 은 <c>2012-01-05</c>.
/// 실무에서 들어오는 표기 흔들림을 흡수하도록 허용 포맷을 열거하되,
/// <b>문화권 의존 파싱은 쓰지 않는다</b>(서버 로캘에 따라 결과가 달라지는 버그를 원천 차단).
/// </remarks>
public static class JoinedDate
{
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd",
        "yyyy.MM.dd",
        "yyyy/MM/dd",
        "yyyyMMdd",
        "yyyy-M-d",
        "yyyy.M.d",
        "yyyy/M/d"
    ];

    /// <summary>허용 포맷 목록(오류 메시지에 노출).</summary>
    public static IReadOnlyList<string> Formats => AcceptedFormats;

    public static bool TryParse(string? input, out DateOnly value)
    {
        value = default;

        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // "2018-03-07T09:30:00Z" 같은 ISO 8601 전체 표기는 날짜 부분만 취한다.
        var separator = trimmed.IndexOf('T', StringComparison.Ordinal);
        if (separator > 0)
        {
            trimmed = trimmed[..separator];
        }

        // TryParse(문화권 추론) 를 쓰지 않는 이유: "07/03/2018" 처럼 일/월 순서가 모호한 표기를
        // 서버 로캘에 따라 다르게 해석해 조용히 틀린 날짜를 저장하게 되기 때문이다. 그런 입력은 거절한다.
        return DateOnly.TryParseExact(trimmed, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
