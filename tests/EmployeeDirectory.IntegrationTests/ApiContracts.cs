using System.Text.Json.Serialization;

namespace EmployeeDirectory.IntegrationTests;

/// <summary>
/// 테스트는 서버의 DTO 타입을 재사용하지 않고 <b>응답 JSON 모양</b>을 별도로 선언한다.
/// 내부 타입을 바꿨을 때 외부 계약이 깨지는 것을 테스트가 알아채도록 하기 위해서다.
/// </summary>
internal sealed record EmployeeResponse(int Id, string Name, string Email, string Tel, DateOnly Joined);

internal sealed record PagedResponse(
    IReadOnlyList<EmployeeResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

internal sealed record RegisterResponse(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("created")] int Created,
    [property: JsonPropertyName("updated")] int Updated,
    [property: JsonPropertyName("totalProcessed")] int TotalProcessed);

internal sealed record ProblemResponse(
    string? Title,
    int? Status,
    string? Detail,
    IReadOnlyList<ProblemError>? Errors);

internal sealed record ProblemError(string Code, string Message);
