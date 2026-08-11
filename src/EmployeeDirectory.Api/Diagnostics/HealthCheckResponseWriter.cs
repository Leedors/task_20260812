using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EmployeeDirectory.Api.Diagnostics;

/// <summary>
/// 헬스체크 결과를 JSON 으로 내려준다.
/// </summary>
/// <remarks>
/// 기본 응답은 <c>Healthy</c> 라는 평문 한 줄뿐이라, 장애 시 "무엇이" 실패했는지 알 수 없다.
/// 개별 검사 항목과 소요시간을 함께 노출해 모니터링 도구가 바로 활용할 수 있게 한다.
/// </remarks>
internal static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // 기본 인코더는 한글을 \uXXXX 로 이스케이프해 사람이 바로 읽기 어렵다.
        // HTML 위험 문자는 그대로 이스케이프하면서 한글만 원문으로 내보낸다.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
            })
        };

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, Options));
    }
}
