namespace EmployeeDirectory.Application.Abstractions.Persistence;

/// <summary>
/// 저장소가 살아 있는지 확인한다(헬스체크용).
/// </summary>
/// <remarks>
/// Api 계층이 <c>DbContext</c> 를 직접 참조하지 않고도 상태를 물어볼 수 있게 하기 위한 최소 인터페이스다.
/// 영속성 기술이 바뀌어도 헬스체크 코드는 그대로다.
/// </remarks>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
