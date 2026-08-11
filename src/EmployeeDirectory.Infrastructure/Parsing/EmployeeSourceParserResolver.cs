using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Infrastructure.Parsing;

/// <summary>
/// 페이로드에 맞는 파서를 고른다.
/// </summary>
/// <remarks>
/// 판별 순서는 <b>선언된 형식 우선, 없으면 내용 추론</b>이다.
/// 파일 업로드는 확장자/Content-Type 으로 형식이 명확하지만,
/// <c>&lt;textarea&gt;</c> 직접 입력은 형식 정보가 없을 수 있어 내용으로 추론해야 한다.
/// 새 형식(xml, xlsx …)을 지원하려면 <see cref="IEmployeeSourceParser"/> 구현을 하나 추가해
/// DI 에 등록하기만 하면 되고, 이 클래스는 수정할 필요가 없다(OCP).
/// </remarks>
internal sealed class EmployeeSourceParserResolver(IEnumerable<IEmployeeSourceParser> parsers)
    : IEmployeeSourceParserResolver
{
    private readonly IReadOnlyList<IEmployeeSourceParser> _parsers = parsers.ToArray();

    public Result<IEmployeeSourceParser> Resolve(EmployeePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Content))
        {
            return Result.Failure<IEmployeeSourceParser>(
                Error.Validation("payload.empty", "요청 본문이 비어 있습니다. csv 또는 json 데이터를 전달해 주세요."));
        }

        if (payload.DeclaredFormat is { } declared)
        {
            var declaredParser = _parsers.FirstOrDefault(parser => parser.Format == declared);

            return declaredParser is null
                ? Result.Failure<IEmployeeSourceParser>(
                    Error.Validation("payload.format_unsupported", $"지원하지 않는 형식입니다: {declared}"))
                : Result.Success(declaredParser);
        }

        // 등록 순서대로 판정한다. json 은 시작 문자로 정확히 구분되고, csv 가 폴백이다.
        var detected = _parsers.FirstOrDefault(parser => parser.CanParse(payload.Content));

        return detected is null
            ? Result.Failure<IEmployeeSourceParser>(
                Error.Validation("payload.format_undetected", "본문의 형식을 판별하지 못했습니다. csv 또는 json 으로 보내주세요."))
            : Result.Success(detected);
    }
}
