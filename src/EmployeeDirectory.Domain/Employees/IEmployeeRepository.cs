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
    /// <summary>주어진 이메일들에 해당하는 기존 직원을 이메일을 키로 반환한다.</summary>
    Task<IReadOnlyDictionary<string, Employee>> FindByEmailsAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<Employee> employees);
}
