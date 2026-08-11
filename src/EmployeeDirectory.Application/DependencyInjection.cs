using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeDirectory.Application;

public static class DependencyInjection
{
    private static readonly Type[] OpenHandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IValidator<>)
    ];

    /// <summary>
    /// 핸들러/검증기를 어셈블리에서 자동 등록하고 파이프라인을 구성한다.
    /// </summary>
    /// <remarks>
    /// 핸들러를 새로 추가할 때 DI 등록을 잊어 런타임에야 터지는 사고를 막기 위해 스캔 방식을 택했다.
    /// 스캔 대상은 이 어셈블리 하나뿐이라 시작 비용은 무시할 수준이다.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Dispatcher>();
        services.AddScoped<ICommandDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
        services.AddScoped<IQueryDispatcher>(sp => sp.GetRequiredService<Dispatcher>());

        RegisterHandlers(services);

        // 등록 순서 = 바깥에서 안쪽 순서. 로깅이 검증 실패까지 관찰할 수 있도록 가장 바깥에 둔다.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        var implementations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            var contracts = implementation
                .GetInterfaces()
                .Where(contract => contract.IsGenericType
                                   && OpenHandlerInterfaces.Contains(contract.GetGenericTypeDefinition()));

            foreach (var contract in contracts)
            {
                services.AddScoped(contract, implementation);
            }
        }
    }
}
