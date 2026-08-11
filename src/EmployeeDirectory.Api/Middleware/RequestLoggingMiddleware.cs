using System.Diagnostics;

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
                "{Method} {Path}{Query} → {StatusCode} ({Elapsed:0.##}ms)",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Response.StatusCode,
                elapsed);
        }
    }
}
