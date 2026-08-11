using EmployeeDirectory.Application.Employees.Dtos;

namespace EmployeeDirectory.Application.Abstractions.Persistence;

/// <summary>
/// 읽기 전용 조회 모델.
/// </summary>
/// <remarks>
/// CQRS 의 실질적인 이득은 여기서 나온다. 조회는 애그리게이트를 거치지 않고
/// 화면이 필요로 하는 DTO 로 바로 투영(projection)한다 —
/// 불필요한 트래킹/지연로딩이 없고, 나중에 조회 전용 저장소(예: 캐시, 검색엔진)로
/// 갈아끼울 때 쓰기 모델을 건드리지 않아도 된다.
/// </remarks>
public interface IEmployeeReadStore
{
    /// <param name="page">1부터 시작하는 페이지 번호.</param>
    /// <param name="pageSize">페이지 크기.</param>
    /// <param name="keyword">
    /// 이름·이메일·전화번호 부분 일치 검색어. 비어 있으면 전체를 대상으로 한다.
    /// </param>
    /// <param name="cancellationToken">취소 토큰.</param>
    Task<PagedResult<EmployeeDto>> GetPageAsync(
        int page,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);

    Task<EmployeeDto?> FindByNameAsync(string name, CancellationToken cancellationToken);
}
