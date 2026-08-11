namespace EmployeeDirectory.Domain.Common;

/// <summary>
/// 실패의 종류. 상위 계층(API)이 HTTP 상태 코드로 번역하기 위한 최소한의 분류다.
/// </summary>
public enum ErrorType
{
    /// <summary>입력값이 규칙을 위반함 → 400</summary>
    Validation,

    /// <summary>대상을 찾을 수 없음 → 404</summary>
    NotFound,

    /// <summary>현재 상태와 충돌함 → 409</summary>
    Conflict
}

/// <summary>
/// "예상 가능한 실패"를 값으로 표현한다.
/// 예외는 <em>예상하지 못한</em> 상황에만 쓰고, 검증 실패/미존재 같은 정상 흐름은 이 타입으로 전달한다.
/// </summary>
/// <param name="Code">기계가 분기할 수 있는 안정적인 식별자 (예: <c>employee.email_invalid</c>)</param>
/// <param name="Message">사람이 읽는 설명</param>
/// <param name="Type">HTTP 매핑을 위한 분류</param>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public override string ToString() => $"{Code}: {Message}";
}
