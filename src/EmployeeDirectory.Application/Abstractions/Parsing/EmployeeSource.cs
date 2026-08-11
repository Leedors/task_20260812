using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Abstractions.Parsing;

/// <summary>지원하는 입력 형식.</summary>
public enum EmployeeSourceFormat
{
    Csv,
    Json
}

/// <summary>
/// 업로드된 직원 데이터. 전송 수단(파일 업로드 / 본문 직접 입력)과 무관한 형태다.
/// </summary>
/// <remarks>
/// 과제의 4가지 요구(csv 파일, json 파일, body csv, body json)를 <b>2×2</b> 로 분해했다.
/// "어떻게 도착했는가"(HTTP 관심사)는 Api 계층이 흡수하고,
/// Application 은 "무슨 형식의 텍스트인가"만 알면 된다. 그래서 엔드포인트는 하나로 충분하다.
/// </remarks>
/// <param name="Content">원본 텍스트.</param>
/// <param name="DeclaredFormat">호출자가 알려준 형식(파일 확장자·Content-Type). 없으면 내용으로 추론한다.</param>
/// <param name="SourceName">로그/오류 메시지용 원본 이름(파일명 등).</param>
public sealed record EmployeePayload(string Content, EmployeeSourceFormat? DeclaredFormat = null, string? SourceName = null);

/// <summary>
/// 파싱 단계의 산출물. 아직 검증 전인 "원시 행"이다.
/// </summary>
/// <remarks>
/// 파서는 형식만 책임지고 값의 유효성은 도메인이 판정한다(관심사 분리).
/// <paramref name="Position"/> 은 csv 는 파일의 행 번호, json 은 배열 인덱스(1-base)로,
/// 실패 메시지에서 사용자가 원본을 바로 찾을 수 있게 한다.
/// </remarks>
public sealed record EmployeeRecord(string? Name, string? Email, string? Tel, string? Joined, int Position);

/// <summary>형식별 파서.</summary>
public interface IEmployeeSourceParser
{
    EmployeeSourceFormat Format { get; }

    /// <summary>선언된 형식이 없을 때 내용만 보고 이 파서가 처리 가능한지 판단한다.</summary>
    bool CanParse(string content);

    Result<IReadOnlyList<EmployeeRecord>> Parse(EmployeePayload payload);
}

/// <summary>페이로드에 맞는 파서를 고른다.</summary>
public interface IEmployeeSourceParserResolver
{
    Result<IEmployeeSourceParser> Resolve(EmployeePayload payload);
}
