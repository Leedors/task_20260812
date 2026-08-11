using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Domain.Employees;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;

/// <summary>
/// 업로드 파이프라인: 형식 판별 → 파싱 → 도메인 검증 → 중복 정리 → 단일 트랜잭션 저장.
/// </summary>
/// <remarks>
/// <para><b>부분 성공을 허용하지 않는다.</b> 한 건이라도 유효하지 않으면 아무것도 저장하지 않고
/// 실패한 항목을 모두 알려준다. 긴급 연락망 데이터가 "절반만 반영된" 상태로 남는 것이
/// 업로드를 다시 시키는 것보다 훨씬 위험하기 때문이다.</para>
/// <para><b>이메일이 같으면 갱신한다(upsert).</b> 연락처 파일을 다시 올리는 것은 실무에서
/// 흔한 갱신 시나리오이므로 409 로 막기보다 최신 값으로 덮어쓰는 편이 자연스럽다.</para>
/// </remarks>
internal sealed class RegisterEmployeesCommandHandler(
    IEmployeeSourceParserResolver parserResolver,
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<RegisterEmployeesCommandHandler> logger)
    : ICommandHandler<RegisterEmployeesCommand, RegisterEmployeesResult>
{
    /// <summary>응답이 무한정 커지지 않도록 오류 상세는 이 개수까지만 돌려준다.</summary>
    private const int MaxReportedErrors = 50;

    public async Task<Result<RegisterEmployeesResult>> HandleAsync(
        RegisterEmployeesCommand command,
        CancellationToken cancellationToken)
    {
        var parserResult = parserResolver.Resolve(command.Payload);
        if (parserResult.IsFailure)
        {
            return Result.Failure<RegisterEmployeesResult>(parserResult.Errors);
        }

        var parser = parserResult.Value;

        var parseResult = parser.Parse(command.Payload);
        if (parseResult.IsFailure)
        {
            return Result.Failure<RegisterEmployeesResult>(Trim(parseResult.Errors));
        }

        var records = parseResult.Value;
        if (records.Count == 0)
        {
            return Result.Failure<RegisterEmployeesResult>(
                Error.Validation("payload.no_records", "등록할 직원 데이터가 없습니다."));
        }

        var conversion = ToEmployees(records, parser.Format);
        if (conversion.IsFailure)
        {
            return Result.Failure<RegisterEmployeesResult>(Trim(conversion.Errors));
        }

        var parsed = conversion.Value;

        var existing = await repository
            .FindByEmailsAsync(parsed.Select(item => item.Email.Value).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var toInsert = new List<Employee>();
        var updated = 0;

        foreach (var item in parsed)
        {
            if (existing.TryGetValue(item.Email.Value, out var current))
            {
                current.UpdateContact(item.Name, item.Tel, item.Joined);
                updated++;
                continue;
            }

            var created = Employee.Create(item.Name, item.Email.Value, item.Tel.Value, item.Joined, dateTimeProvider.Today);
            if (created.IsFailure)
            {
                // ToEmployees 에서 이미 검증된 값이므로 도달하지 않는다. 방어적으로만 남긴다.
                return Result.Failure<RegisterEmployeesResult>(created.Errors);
            }

            toInsert.Add(created.Value);
        }

        repository.AddRange(toInsert);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "직원 {Total}건 등록 완료 (형식: {Format}, 신규: {Created}, 갱신: {Updated}, 원본: {Source})",
            parsed.Count,
            parser.Format,
            toInsert.Count,
            updated,
            command.Payload.SourceName ?? "(body)");

        return Result.Success(new RegisterEmployeesResult(parser.Format, toInsert.Count, updated, parsed.Count));
    }

    /// <summary>
    /// 원시 행을 도메인 값으로 변환한다. 실패는 <b>모아서</b> 반환한다(첫 실패에서 멈추지 않는다).
    /// </summary>
    private Result<IReadOnlyList<ParsedEmployee>> ToEmployees(
        IReadOnlyList<EmployeeRecord> records,
        EmployeeSourceFormat format)
    {
        var today = dateTimeProvider.Today;
        var errors = new List<Error>();
        var byEmail = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parsed = new List<ParsedEmployee>(records.Count);

        foreach (var record in records)
        {
            if (!JoinedDate.TryParse(record.Joined, out var joined))
            {
                errors.Add(AtPosition(
                    format,
                    record.Position,
                    Error.Validation(
                        "employee.joined_invalid",
                        $"입사일 형식이 올바르지 않습니다: '{record.Joined}' (허용 형식: {string.Join(", ", JoinedDate.Formats)})")));
                continue;
            }

            var employee = Employee.Create(record.Name, record.Email, record.Tel, joined, today);
            if (employee.IsFailure)
            {
                errors.AddRange(employee.Errors.Select(error => AtPosition(format, record.Position, error)));
                continue;
            }

            var email = employee.Value.Email.Value;
            if (byEmail.TryGetValue(email, out var firstPosition))
            {
                errors.Add(AtPosition(
                    format,
                    record.Position,
                    Error.Validation(
                        "employee.email_duplicated_in_payload",
                        $"이메일 '{email}' 이(가) 같은 요청 안에서 중복됩니다(최초 {PositionLabel(format, firstPosition)}).")));
                continue;
            }

            byEmail[email] = record.Position;
            parsed.Add(new ParsedEmployee(
                employee.Value.Name,
                employee.Value.Email,
                employee.Value.Tel,
                employee.Value.Joined));
        }

        return errors.Count > 0
            ? Result.Failure<IReadOnlyList<ParsedEmployee>>(errors)
            : Result.Success<IReadOnlyList<ParsedEmployee>>(parsed);
    }

    /// <summary>
    /// 오류 메시지에 원본 위치를 붙인다. csv 는 파일의 행 번호, json 은 배열 안의 순번이 자연스러운 표현이다.
    /// </summary>
    private static Error AtPosition(EmployeeSourceFormat format, int position, Error error)
        => error with { Message = $"[{PositionLabel(format, position)}] {error.Message}" };

    private static string PositionLabel(EmployeeSourceFormat format, int position)
        => format == EmployeeSourceFormat.Csv ? $"{position}행" : $"{position}번째 항목";

    private static IReadOnlyList<Error> Trim(IReadOnlyList<Error> errors)
    {
        if (errors.Count <= MaxReportedErrors)
        {
            return errors;
        }

        var trimmed = errors.Take(MaxReportedErrors).ToList();
        trimmed.Add(Error.Validation(
            "payload.too_many_errors",
            $"오류가 {errors.Count}건 발생하여 앞의 {MaxReportedErrors}건만 표시합니다."));

        return trimmed;
    }

    private sealed record ParsedEmployee(string Name, EmailAddress Email, PhoneNumber Tel, DateOnly Joined);
}
