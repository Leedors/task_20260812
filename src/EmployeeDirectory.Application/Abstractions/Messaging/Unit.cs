namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>
/// "반환할 값이 없음"을 나타내는 타입.
/// </summary>
/// <remarks>
/// <c>ICommand&lt;TResponse&gt;</c> 는 항상 응답 타입을 요구한다. 삭제처럼 돌려줄 것이 없는 커맨드를 위해
/// <c>void</c> 전용 인터페이스를 하나 더 만들면 디스패처·파이프라인이 전부 두 벌이 된다.
/// 값이 없다는 사실을 타입으로 표현하는 편이 전체 구조를 단순하게 유지한다.
/// </remarks>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
