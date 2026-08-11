namespace EmployeeDirectory.Api.Contracts;

/// <summary>
/// 직원 연락처 수정 요청. PUT 시맨틱이므로 <b>모든 필드를 보내야</b> 합니다.
/// </summary>
/// <param name="Name">이름.</param>
/// <param name="Email">이메일. 다른 직원이 쓰고 있으면 409 를 반환합니다.</param>
/// <param name="Tel">전화번호. 하이픈 유무는 상관없습니다.</param>
/// <param name="Joined">입사일. yyyy-MM-dd, yyyy.MM.dd 등을 허용합니다.</param>
public sealed record UpdateEmployeeRequest(string? Name, string? Email, string? Tel, string? Joined);
