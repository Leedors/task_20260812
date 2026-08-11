namespace EmployeeDirectory.Application.Abstractions.Time;

/// <summary>
/// 현재 시각 공급자. 테스트에서 "오늘"을 고정하기 위해 추상화한다.
/// </summary>
public interface IDateTimeProvider
{
    DateOnly Today { get; }
}
