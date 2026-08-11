using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeDirectory.Api.Common;

/// <summary>
/// 처리되지 않은 예외를 problem+json 으로 변환한다.
/// </summary>
/// <remarks>
/// 예상 가능한 실패는 <see cref="Domain.Common.Result"/> 로 처리하므로 여기까지 오는 것은 모두 "버그"다.
/// 따라서 내부 메시지를 노출하지 않고(정보 노출 방지) 로그에만 전체 스택을 남긴다.
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "처리되지 않은 예외 발생: {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "서버 내부 오류",
                Detail = "요청을 처리하는 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요."
            }
        }).ConfigureAwait(false);
    }
}
