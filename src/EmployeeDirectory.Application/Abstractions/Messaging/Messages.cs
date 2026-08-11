using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>상태를 바꾸는 요청.</summary>
public interface ICommand<TResponse>;

/// <summary>상태를 바꾸지 않는 요청.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>파이프라인에서 다음 단계(최종적으로는 핸들러)를 호출하는 델리게이트.</summary>
public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>
/// 핸들러 실행을 감싸는 횡단 관심사(로깅, 검증, 트랜잭션 등).
/// </summary>
/// <remarks>
/// 핸들러 본문을 건드리지 않고 동작을 추가할 수 있어야 "설계 변경 반영이 쉬운 코드"가 된다.
/// </remarks>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
