using EmployeeDirectory.Application.Abstractions.Time;

namespace EmployeeDirectory.Infrastructure.Time;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// 입사일 판정은 한국 기준 날짜여야 하므로 서버 로컬 시간대가 아닌 KST 로 고정한다.
    /// (UTC 서버에 배포되면 자정 무렵 하루가 어긋나는 문제를 막는다.)
    /// </summary>
    private static readonly TimeZoneInfo KoreaTimeZone = ResolveKoreaTimeZone();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KoreaTimeZone));

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    private static TimeZoneInfo ResolveKoreaTimeZone()
    {
        // Windows 와 Linux 의 시간대 ID 가 달라 둘 다 시도한다.
        foreach (var id in new[] { "Korea Standard Time", "Asia/Seoul" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // 다음 후보 시도
            }
            catch (InvalidTimeZoneException)
            {
                // 다음 후보 시도
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
    }
}
