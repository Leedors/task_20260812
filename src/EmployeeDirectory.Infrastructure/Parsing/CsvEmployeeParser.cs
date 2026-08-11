using System.Text;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Infrastructure.Parsing;

/// <summary>
/// csv 파서.
/// </summary>
/// <remarks>
/// <para>외부 CSV 라이브러리를 쓰지 않은 이유: 요구되는 형식이 "한 줄 = 한 명"으로 단순하고,
/// 헤더 유무 자동 판별처럼 과제 특유의 규칙이 필요해 직접 제어하는 편이 명확하기 때문이다.</para>
/// <para>지원 범위: 따옴표로 감싼 필드와 <c>""</c> 이스케이프, 앞뒤 공백 제거, 빈 줄/<c>#</c> 주석 무시,
/// 헤더가 있으면 컬럼명 매핑 · 없으면 위치 기반 매핑, 필수 4개 외 추가 컬럼 허용.</para>
/// <para>지원하지 않는 것: 따옴표 안에서 줄바꿈이 포함된 멀티라인 필드.
/// 연락망 데이터에는 나타나지 않는 형태라 의도적으로 제외했다.</para>
/// </remarks>
internal sealed class CsvEmployeeParser : IEmployeeSourceParser
{
    private const int RequiredColumnCount = 4;

    private static readonly string[] NameAliases = ["name", "이름", "성명", "employee_name", "employeename"];
    private static readonly string[] EmailAliases = ["email", "e-mail", "mail", "이메일"];
    private static readonly string[] TelAliases = ["tel", "phone", "mobile", "phone_number", "phonenumber", "전화번호", "연락처"];
    private static readonly string[] JoinedAliases = ["joined", "joined_at", "joindate", "join_date", "hire_date", "입사일", "입사일자"];

    public EmployeeSourceFormat Format => EmployeeSourceFormat.Csv;

    /// <summary>형식 추론에서 최후의 폴백 역할을 한다(json 이 아니면 csv 로 간주).</summary>
    public bool CanParse(string content) => !string.IsNullOrWhiteSpace(content);

    public Result<IReadOnlyList<EmployeeRecord>> Parse(EmployeePayload payload)
    {
        var content = payload.Content.TrimStart('﻿');
        var lines = content.Split('\n');

        var rows = new List<(string[] Fields, int LineNumber)>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            rows.Add((SplitFields(line), index + 1));
        }

        if (rows.Count == 0)
        {
            return Result.Failure<IReadOnlyList<EmployeeRecord>>(
                Error.Validation("csv.empty", "csv 에서 읽을 수 있는 행이 없습니다."));
        }

        var columnMap = TryBuildHeaderMap(rows[0].Fields);
        var dataRows = columnMap is null ? rows : rows.Skip(1).ToList();

        if (dataRows.Count == 0)
        {
            return Result.Failure<IReadOnlyList<EmployeeRecord>>(
                Error.Validation("csv.header_only", "csv 에 헤더만 있고 데이터 행이 없습니다."));
        }

        var records = new List<EmployeeRecord>(dataRows.Count);
        var errors = new List<Error>();

        foreach (var (fields, lineNumber) in dataRows)
        {
            if (columnMap is null)
            {
                if (fields.Length < RequiredColumnCount)
                {
                    errors.Add(Error.Validation(
                        "csv.column_missing",
                        $"[{lineNumber}행] 컬럼이 {fields.Length}개입니다. 최소 {RequiredColumnCount}개(이름, 이메일, 전화번호, 입사일)가 필요합니다."));
                    continue;
                }

                records.Add(new EmployeeRecord(fields[0], fields[1], fields[2], fields[3], lineNumber));
                continue;
            }

            records.Add(new EmployeeRecord(
                Value(fields, columnMap.Name),
                Value(fields, columnMap.Email),
                Value(fields, columnMap.Tel),
                Value(fields, columnMap.Joined),
                lineNumber));
        }

        return errors.Count > 0
            ? Result.Failure<IReadOnlyList<EmployeeRecord>>(errors)
            : Result.Success<IReadOnlyList<EmployeeRecord>>(records);
    }

    private static string? Value(string[] fields, int index)
        => index >= 0 && index < fields.Length ? fields[index] : null;

    /// <summary>
    /// 첫 행이 헤더인지 판별한다. 필수 4개 컬럼명을 모두 찾을 수 있을 때만 헤더로 인정한다.
    /// (일부만 일치하면 데이터 행일 가능성이 높아 위치 기반으로 처리하는 편이 안전하다.)
    /// </summary>
    private static ColumnMap? TryBuildHeaderMap(string[] fields)
    {
        var normalized = fields.Select(field => field.Trim().ToLowerInvariant()).ToArray();

        var name = IndexOfAlias(normalized, NameAliases);
        var email = IndexOfAlias(normalized, EmailAliases);
        var tel = IndexOfAlias(normalized, TelAliases);
        var joined = IndexOfAlias(normalized, JoinedAliases);

        return name >= 0 && email >= 0 && tel >= 0 && joined >= 0
            ? new ColumnMap(name, email, tel, joined)
            : null;
    }

    private static int IndexOfAlias(string[] normalizedFields, string[] aliases)
        => Array.FindIndex(normalizedFields, field => aliases.Contains(field, StringComparer.Ordinal));

    /// <summary>RFC 4180 의 따옴표 규칙을 지원하는 한 줄 분해기.</summary>
    private static string[] SplitFields(string line)
    {
        var fields = new List<string>();
        var buffer = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (inQuotes)
            {
                if (character != '"')
                {
                    buffer.Append(character);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    buffer.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(buffer.ToString().Trim());
                    buffer.Clear();
                    break;
                default:
                    buffer.Append(character);
                    break;
            }
        }

        fields.Add(buffer.ToString().Trim());
        return fields.ToArray();
    }

    private sealed record ColumnMap(int Name, int Email, int Tel, int Joined);
}
