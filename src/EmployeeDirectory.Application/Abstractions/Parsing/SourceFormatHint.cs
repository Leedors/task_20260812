namespace EmployeeDirectory.Application.Abstractions.Parsing;

/// <summary>
/// 파일명·Content-Type 같은 "힌트"에서 형식을 읽어낸다.
/// </summary>
/// <remarks>
/// 힌트일 뿐이므로 판별에 실패하면 <c>null</c> 을 돌려주고, 최종 판정은 내용 추론에 맡긴다.
/// </remarks>
public static class SourceFormatHint
{
    public static EmployeeSourceFormat? FromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" => EmployeeSourceFormat.Csv,
            ".json" => EmployeeSourceFormat.Json,
            _ => null
        };
    }

    public static EmployeeSourceFormat? FromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        // "text/csv; charset=utf-8" 처럼 파라미터가 붙어 오는 경우를 잘라낸다.
        var mediaType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();

        return mediaType switch
        {
            "text/csv" or "application/csv" or "text/comma-separated-values" => EmployeeSourceFormat.Csv,
            "application/json" or "text/json" or "application/problem+json" => EmployeeSourceFormat.Json,
            // text/plain, application/octet-stream 등은 형식을 단정할 수 없으므로 내용 추론에 맡긴다.
            _ => null
        };
    }
}
