namespace EmployeeDirectory.Domain.Employees;

/// <summary>
/// 쓰기 모델용 저장소. (읽기는 <c>IEmployeeReadStore</c> 가 따로 담당한다 — CQRS)
/// </summary>
/// <remarks>
/// 저장소 인터페이스는 애그리게이트 단위 조작만 노출한다.
/// 조회용 <c>IQueryable</c> 을 외부로 흘리지 않아야 영속성 기술 교체가 가능해진다.
/// </remarks>
public interface IEmployeeRepository
{
    /// <summary>
    /// 주어진 이메일들에 해당하는 기존 직원을 이메일을 키로 반환한다.
    /// </summary>
    /// <remarks>
    /// 삭제된 직원도 포함한다. 이메일에 유일 제약이 걸려 있어, 삭제된 사람을 빼고 조회하면
    /// 같은 이메일 재업로드가 유일 제약 위반으로 실패하기 때문이다(재입사 시나리오).
    /// </remarks>
    Task<IReadOnlyDictionary<string, Employee>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken);

    /// <summary>삭제되지 않은 직원을 식별자로 찾는다.</summary>
    Task<Employee?> FindByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// 이메일이 이미 다른 직원에게 쓰이고 있는지 확인한다(수정 시 충돌 판정용).
    /// </summary>
    /// <param name="email">확인할 이메일(정규화된 값).</param>
    /// <param name="excludedId">자기 자신은 충돌 대상에서 제외한다.</param>
    /// <param name="cancellationToken">취소 토큰.</param>
    Task<bool> ExistsByEmailAsync(string email, int excludedId, CancellationToken cancellationToken);

    void AddRange(IEnumerable<Employee> employees);
}
