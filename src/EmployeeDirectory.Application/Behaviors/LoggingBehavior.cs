using System.Diagnostics;
using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Application.Behaviors;

/// <summary>
/// 모든 커맨드/쿼리의 시작·종료·소요시간을 구조적 로그로 남긴다.
/// </summary>
/// <remarks>
/// 핸들러마다 로깅 코드를 중복해서 넣지 않기 위해 파이프라인으로 뽑았다.
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var timestamp = Stopwatch.GetTimestamp();

        logger.LogInformation("{RequestName} 처리 시작", requestName);

        try
        {
            var result = await next().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            if (result.IsSuccess)
            {
                logger.LogInformation("{RequestName} 처리 성공 ({Elapsed:0.##}ms)", requestName, elapsed);
            }
            else
            {
                // 오류 메시지에는 잘못된 이메일·전화번호가 그대로 들어 있다.
                // 로그에는 분류가 가능한 코드만 남기고, 상세 메시지는 응답(요청자)에게만 돌려준다.
                logger.LogWarning(
                    "{RequestName} 처리 실패 ({Elapsed:0.##}ms) - {ErrorCount}건: {ErrorCodes}",
                    requestName,
                    elapsed,
                    result.Errors.Count,
                    string.Join(", ", result.Errors.Select(error => error.Code).Distinct().Take(5)));
            }

            return result;
        }
        catch (Exception ex)
        {
            var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            logger.LogError(ex, "{RequestName} 처리 중 예외 발생 ({Elapsed:0.##}ms)", requestName, elapsed);
            throw;
        }
    }
}
