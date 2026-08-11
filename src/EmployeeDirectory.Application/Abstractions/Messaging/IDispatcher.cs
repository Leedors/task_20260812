using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>커맨드를 해당 핸들러로 보낸다.</summary>
/// <remarks>
/// 요청 타입을 제네릭 인자로 <b>명시</b>하게 만든 이유:
/// <c>SendAsync(ICommand&lt;TResponse&gt;)</c> 형태로 받으면 구체 타입을 런타임에 알아내야 해서
/// 리플렉션이 필요해진다. 호출부가 조금 장황해지는 대신 어떤 핸들러가 실행될지가
/// 컴파일 시점에 전부 결정되고, 디스패처 구현에서 리플렉션이 사라진다.
/// </remarks>
public interface ICommandDispatcher
{
    Task<Result<TResponse>> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>;
}

/// <summary>쿼리를 해당 핸들러로 보낸다.</summary>
/// <remarks>
/// 메서드 이름이 <c>SendAsync</c> 가 아닌 이유는 단순하다. 두 인터페이스를 한 클래스가 구현하는데
/// 제네릭 인자 개수와 파라미터가 같으면 시그니처가 충돌한다. 이름을 나누는 편이 호출부에서도 읽기 쉽다.
/// </remarks>
public interface IQueryDispatcher
{
    Task<Result<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>;
}
