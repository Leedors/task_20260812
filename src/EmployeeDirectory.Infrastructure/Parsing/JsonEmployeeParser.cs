using System.Text.Json;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Infrastructure.Parsing;

/// <summary>
/// json 파서.
/// </summary>
/// <remarks>
/// <para>POCO 로 역직렬화하지 않고 <see cref="JsonDocument"/> 로 직접 읽는 이유:
/// 프로퍼티 이름을 대소문자/별칭까지 유연하게 받아들이고, 타입 불일치(예: tel 이 숫자)를
/// 예외가 아니라 <see cref="Result"/> 실패로 다루기 위해서다.</para>
/// <para>루트가 배열이면 여러 건, 객체면 한 건으로 처리한다.
/// 필수 필드 외의 프로퍼티는 무시한다.</para>
/// </remarks>
internal sealed class JsonEmployeeParser : IEmployeeSourceParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly string[] NameAliases = ["name", "employeename", "employee_name", "이름"];
    private static readonly string[] EmailAliases = ["email", "e-mail", "mail", "이메일"];
    private static readonly string[] TelAliases = ["tel", "phone", "mobile", "phonenumber", "phone_number", "전화번호", "연락처"];
    private static readonly string[] JoinedAliases = ["joined", "joinedat", "joined_at", "joindate", "join_date", "hiredate", "입사일"];

    public EmployeeSourceFormat Format => EmployeeSourceFormat.Json;

    public bool CanParse(string content)
    {
        var trimmed = content.AsSpan().TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '[' || trimmed[0] == '{');
    }

    public Result<IReadOnlyList<EmployeeRecord>> Parse(EmployeePayload payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload.Content, DocumentOptions);
        }
        catch (JsonException ex)
        {
            return Result.Failure<IReadOnlyList<EmployeeRecord>>(
                Error.Validation("json.malformed", $"json 을 해석할 수 없습니다: {ex.Message}"));
        }

        using (document)
        {
            var root = document.RootElement;

            return root.ValueKind switch
            {
                JsonValueKind.Array => ReadArray(root),
                JsonValueKind.Object => ReadSingle(root),
                _ => Result.Failure<IReadOnlyList<EmployeeRecord>>(
                    Error.Validation("json.unexpected_root", "json 최상위는 배열 또는 객체여야 합니다."))
            };
        }
    }

    private static Result<IReadOnlyList<EmployeeRecord>> ReadSingle(JsonElement element)
        => Result.Success<IReadOnlyList<EmployeeRecord>>([ReadObject(element, position: 1)]);

    private static Result<IReadOnlyList<EmployeeRecord>> ReadArray(JsonElement array)
    {
        var records = new List<EmployeeRecord>(array.GetArrayLength());
        var errors = new List<Error>();
        var position = 0;

        foreach (var element in array.EnumerateArray())
        {
            position++;

            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add(Error.Validation(
                    "json.unexpected_element",
                    $"[{position}번째 항목] 배열 요소는 객체여야 합니다(실제: {element.ValueKind})."));
                continue;
            }

            records.Add(ReadObject(element, position));
        }

        return errors.Count > 0
            ? Result.Failure<IReadOnlyList<EmployeeRecord>>(errors)
            : Result.Success<IReadOnlyList<EmployeeRecord>>(records);
    }

    private static EmployeeRecord ReadObject(JsonElement element, int position)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            properties[property.Name] = property.Value;
        }

        return new EmployeeRecord(
            Read(properties, NameAliases),
            Read(properties, EmailAliases),
            Read(properties, TelAliases),
            Read(properties, JoinedAliases),
            position);
    }

    private static string? Read(Dictionary<string, JsonElement> properties, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (!properties.TryGetValue(alias, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                // 숫자로 표기된 전화번호 등도 문자열로 받아들인다.
                _ => value.GetRawText()
            };
        }

        return null;
    }
}
