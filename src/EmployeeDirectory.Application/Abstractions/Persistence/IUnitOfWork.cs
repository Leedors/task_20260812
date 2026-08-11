namespace EmployeeDirectory.Application.Abstractions.Persistence;

/// <summary>
/// 하나의 요청에서 발생한 변경을 한 번에 커밋한다.
/// </summary>
/// <remarks>
/// 업로드는 "전부 성공 아니면 전부 실패"여야 하므로, 저장 시점을 저장소가 아닌 핸들러가 통제한다.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
