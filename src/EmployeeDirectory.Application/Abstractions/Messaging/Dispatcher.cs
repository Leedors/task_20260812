using System.Collections.Concurrent;
using EmployeeDirectory.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>
/// 요청 타입에 맞는 핸들러를 DI 에서 찾아 파이프라인으로 감싼 뒤 실행한다.
/// </summary>
/// <remarks>
/// <para>
/// MediatR 같은 외부 라이브러리를 쓰지 않고 직접 구현한 이유:
/// (1) 필요한 기능이 "요청 → 핸들러 + 파이프라인" 뿐이라 200줄이면 충분하고,
/// (2) 라이선스/버전 정책 같은 외부 변수에 과제가 묶이지 않으며,
/// (3) 면접에서 CQRS 동작 원리를 코드로 직접 설명할 수 있기 때문이다.
/// </para>
/// <para>
/// 리플렉션은 요청 타입당 <b>한 번</b>만 수행하고 래퍼 인스턴스를 캐시한다.
/// </para>
/// </remarks>
internal sealed class Dispatcher(IServiceProvider serviceProvider) : ICommandDispatcher, IQueryDispatcher
{
    private static readonly ConcurrentDictionary<(Type Request, Type Response), object> WrapperCache = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wrapper = (RequestWrapper<TResponse>)GetWrapper(
            command.GetType(),
            typeof(TResponse),
            typeof(CommandWrapper<,>));

        return wrapper.HandleAsync(command, serviceProvider, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wrapper = (RequestWrapper<TResponse>)GetWrapper(
            query.GetType(),
            typeof(TResponse),
            typeof(QueryWrapper<,>));

        return wrapper.HandleAsync(query, serviceProvider, cancellationToken);
    }

    private static object GetWrapper(Type requestType, Type responseType, Type openWrapperType)
        => WrapperCache.GetOrAdd(
            (requestType, responseType),
            static (key, openType) => Activator.CreateInstance(openType.MakeGenericType(key.Request, key.Response))!,
            openWrapperType);

    /// <summary>
    /// 구체 요청 타입을 제네릭 인자로 "닫아" 주는 래퍼.
    /// 덕분에 파이프라인 조립 로직은 리플렉션 없이 강타입으로 작성된다.
    /// </summary>
    private abstract class RequestWrapper<TResponse>
    {
        public abstract Task<Result<TResponse>> HandleAsync(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);

        /// <summary>등록된 behavior 들을 역순으로 감아 최종 실행 델리게이트를 만든다.</summary>
        protected static RequestHandlerDelegate<TResponse> BuildPipeline<TRequest>(
            TRequest request,
            IServiceProvider serviceProvider,
            RequestHandlerDelegate<TResponse> handler,
            CancellationToken cancellationToken)
        {
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

            var next = handler;
            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var current = next;
                next = () => behavior.HandleAsync(request, current, cancellationToken);
            }

            return next;
        }
    }

    private sealed class CommandWrapper<TCommand, TResponse> : RequestWrapper<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<Result<TResponse>> HandleAsync(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var command = (TCommand)request;
            var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();

            return BuildPipeline(
                command,
                serviceProvider,
                () => handler.HandleAsync(command, cancellationToken),
                cancellationToken)();
        }
    }

    private sealed class QueryWrapper<TQuery, TResponse> : RequestWrapper<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<Result<TResponse>> HandleAsync(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var query = (TQuery)request;
            var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResponse>>();

            return BuildPipeline(
                query,
                serviceProvider,
                () => handler.HandleAsync(query, cancellationToken),
                cancellationToken)();
        }
    }
}
