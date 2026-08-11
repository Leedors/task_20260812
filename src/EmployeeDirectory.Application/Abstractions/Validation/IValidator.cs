using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Abstractions.Validation;

/// <summary>
/// 요청 자체의 형식 검증(도메인 규칙이 아닌 "입력 계약" 검증).
/// </summary>
/// <remarks>
/// FluentValidation 을 쓰지 않은 이유는 검증 규칙이 두어 개뿐이고,
/// 외부 의존성을 늘리는 것보다 인터페이스 하나로 끝내는 편이 과제 범위에 맞기 때문이다.
/// 규칙이 늘어나면 이 인터페이스 구현만 교체하면 된다.
/// </remarks>
public interface IValidator<in T>
{
    IReadOnlyList<Error> Validate(T instance);
}
