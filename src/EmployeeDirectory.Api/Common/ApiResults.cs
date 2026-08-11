using System.Diagnostics;
using EmployeeDirectory.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeDirectory.Api.Common;

/// <summary>
/// <see cref="Result"/> 의 실패를 HTTP 응답으로 번역한다.
/// </summary>
/// <remarks>
/// 이 변환을 한 곳에 모아두면 컨트롤러마다 상태 코드 규칙이 제각각이 되는 것을 막을 수 있다.
/// 응답 본문은 RFC 9457(problem+json) 형식을 따르고, 상세 실패 목록은 <c>errors</c> 확장 필드에 담는다.
/// </remarks>
internal static class ApiResults
{
    public static ActionResult Problem(Result result, HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("성공한 Result 로 오류 응답을 만들 수 없습니다.");
        }

        var status = ResolveStatusCode(result.Errors);

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = status switch
            {
                StatusCodes.Status404NotFound => "요청한 리소스를 찾을 수 없습니다.",
                StatusCodes.Status409Conflict => "현재 상태와 충돌하는 요청입니다.",
                _ => "요청이 올바르지 않습니다."
            },
            Detail = result.Errors[0].Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["errors"] = result.Errors
            .Select(error => new ApiError(error.Code, error.Message))
            .ToArray();

        return new ObjectResult(problemDetails)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>가장 "무거운" 실패 종류를 대표 상태 코드로 삼는다.</summary>
    private static int ResolveStatusCode(IReadOnlyList<Error> errors)
    {
        if (errors.Any(error => error.Type == ErrorType.NotFound))
        {
            return StatusCodes.Status404NotFound;
        }

        return errors.Any(error => error.Type == ErrorType.Conflict)
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
    }

    /// <param name="Code">기계 판독용 오류 코드.</param>
    /// <param name="Message">사람이 읽는 메시지.</param>
    private sealed record ApiError(string Code, string Message);
}
