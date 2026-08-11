using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace EmployeeDirectory.Api.Middleware;

/// <summary>
/// 요청 단위 상관관계 ID를 부여하고 처리 결과·소요시간을 남긴다.
/// </summary>
/// <remarks>
/// 장애 분석에서 "어떤 요청의 로그인가"를 묶어보려면 상관관계 ID가 필요하다.
/// 클라이언트가 <c>X-Correlation-Id</c> 를 보내면 그대로 이어받고, 없으면 새로 발급해 응답 헤더로 돌려준다.
/// </remarks>
internal sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var provided)
                            && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            // 4xx/5xx 는 조사 대상이므로 레벨을 올린다.
            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                : context.Response.StatusCode >= 400 ? LogLevel.Warning
                : LogLevel.Information;

            logger.Log(
                level,
                "{Method} {Route}{QueryKeys} → {StatusCode} ({Elapsed:0.##}ms)",
                context.Request.Method,
                ResolveRoute(context),
                ResolveQueryKeys(context),
                context.Response.StatusCode,
                elapsed);
        }
    }

    /// <summary>
    /// 원본 경로 대신 라우트 템플릿을 남긴다.
    /// </summary>
    /// <remarks>
    /// <c>GET /api/employee/김철수</c> 를 그대로 남기면 <b>직원 이름이 로그에 쌓인다.</b>
    /// 라우팅이 끝난 시점(응답 직전)이므로 매칭된 엔드포인트에서 템플릿을 꺼낼 수 있다.
    /// 매칭되는 엔드포인트가 없는 요청(404)은 우리 API 경로가 아니므로 원본을 남겨 조사에 쓴다.
    /// </remarks>
    private static string ResolveRoute(HttpContext context)
    {
        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

        return template is null ? context.Request.Path.Value ?? "/" : $"/{template}";
    }

    /// <summary>
    /// 쿼리스트링은 키만 남긴다.
    /// </summary>
    /// <remarks>검색어(<c>?q=김철수</c>)에도 개인정보가 들어올 수 있기 때문이다.</remarks>
    private static string ResolveQueryKeys(HttpContext context)
        => context.Request.Query.Count == 0
            ? string.Empty
            : $"?{string.Join("&", context.Request.Query.Keys)}";
}
