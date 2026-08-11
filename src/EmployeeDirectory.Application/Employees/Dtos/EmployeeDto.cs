namespace EmployeeDirectory.Application.Employees.Dtos;

/// <summary>
/// 화면에 내려주는 직원 연락처.
/// </summary>
/// <param name="Id">내부 식별자.</param>
/// <param name="Name">이름.</param>
/// <param name="Email">이메일.</param>
/// <param name="Tel">하이픈 표기 전화번호 (예: 010-7531-2468).</param>
/// <param name="Joined">입사일 (yyyy-MM-dd).</param>
public sealed record EmployeeDto(int Id, string Name, string Email, string Tel, DateOnly Joined);
