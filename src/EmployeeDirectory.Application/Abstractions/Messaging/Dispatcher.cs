using EmployeeDirectory.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>
/// 요청 타입에 맞는 핸들러를 DI 에서 찾아 파이프라인으로 감싼 뒤 실행한다.
/// </summary>
/// <remarks>
/// <para>
/// MediatR 같은 외부 라이브러리를 쓰지 않고 직접 구현한 이유:
/// (1) 필요한 기능이 "요청 → 핸들러 + 파이프라인" 뿐이라 이 정도 코드면 충분하고,
/// (2) 라이선스/버전 정책 같은 외부 변수에 묶이지 않으며,
/// (3) 무엇보다 동작을 코드로 직접 설명할 수 있기 때문이다.
/// </para>
/// <para>
/// 리플렉션은 쓰지 않는다. 호출부가 요청 타입을 제네릭 인자로 넘기므로
/// 핸들러 조회도 파이프라인 조립도 전부 강타입으로 처리된다.
/// </para>
/// </remarks>
internal sealed class Dispatcher(IServiceProvider serviceProvider) : ICommandDispatcher, IQueryDispatcher
{
    public Task<Result<TResponse>> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();

        return RunPipeline<TCommand, TResponse>(
            command,
            () => handler.HandleAsync(command, cancellationToken),
            cancellationToken);
    }

    public Task<Result<TResponse>> QueryAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();

        return RunPipeline<TQuery, TResponse>(
            query,
            () => handler.HandleAsync(query, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 등록된 behavior 들로 핸들러를 감싼 뒤 실행한다.
    /// </summary>
    /// <remarks>
    /// 뒤에서부터 감는 이유: 마지막에 만들어진 델리게이트가 가장 먼저 실행된다.
    /// 역순으로 감아야 "DI 등록 순서 = 바깥에서 안쪽 순서"가 된다.
    /// (Logging 을 먼저 등록했으므로 Logging 이 가장 바깥이고, 그래서 검증 실패까지 관찰할 수 있다.)
    /// </remarks>
    private Task<Result<TResponse>> RunPipeline<TRequest, TResponse>(
        TRequest request,
        RequestHandlerDelegate<TResponse> handler,
        CancellationToken cancellationToken)
    {
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

        var next = handler;
        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var current = next;
            next = () => behavior.HandleAsync(request, current, cancellationToken);
        }

        return next();
    }
}
