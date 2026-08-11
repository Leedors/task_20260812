namespace EmployeeDirectory.Application.Abstractions.Time;

/// <summary>
/// 현재 시각 공급자. 테스트에서 "오늘"과 "지금"을 고정하기 위해 추상화한다.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>한국 기준 오늘 날짜. 입사일이 미래인지 판정할 때 쓴다.</summary>
    DateOnly Today { get; }

    /// <summary>
    /// 감사 필드(생성·수정·삭제 시각)용 현재 시각.
    /// </summary>
    /// <remarks>
    /// 기록용 시각은 배포 지역과 무관하게 비교·정렬 가능해야 하므로 UTC 기준 오프셋을 유지한다.
    /// (표시할 때 지역 시간대로 바꾸는 것은 클라이언트의 몫)
    /// </remarks>
    DateTimeOffset UtcNow { get; }
}
