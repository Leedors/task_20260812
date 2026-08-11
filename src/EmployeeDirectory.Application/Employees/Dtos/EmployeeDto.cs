namespace EmployeeDirectory.Application.Employees.Dtos;

/// <summary>
/// 화면에 내려주는 직원 연락처.
/// </summary>
/// <param name="Id">내부 식별자. 수정/삭제 API 의 키로 쓴다.</param>
/// <param name="Name">이름.</param>
/// <param name="Email">이메일.</param>
/// <param name="Tel">하이픈 표기 전화번호 (예: 010-7531-2468).</param>
/// <param name="Joined">입사일 (yyyy-MM-dd).</param>
/// <param name="CreatedAt">등록 시각.</param>
/// <param name="UpdatedAt">
/// 마지막 수정 시각. 긴급 연락망에서는 "이 번호가 얼마나 오래된 정보인가"가
/// 번호 그 자체만큼 중요하므로 목록에도 함께 내려준다.
/// </param>
public sealed record EmployeeDto(
    int Id,
    string Name,
    string Email,
    string Tel,
    DateOnly Joined,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
