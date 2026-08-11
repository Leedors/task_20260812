using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Parsing;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Employees.Commands.RegisterEmployees;

/// <summary>csv/json 페이로드로 직원 연락처를 일괄 등록한다.</summary>
public sealed record RegisterEmployeesCommand(EmployeePayload Payload) : ICommand<RegisterEmployeesResult>;

/// <summary>
/// 등록 결과 요약.
/// </summary>
/// <param name="Format">실제로 사용된 파서 형식.</param>
/// <param name="Created">새로 추가된 건수.</param>
/// <param name="Updated">이미 있는 이메일이라 갱신된 건수.</param>
/// <param name="TotalProcessed">페이로드에서 읽어들인 총 건수.</param>
public sealed record RegisterEmployeesResult(
    EmployeeSourceFormat Format,
    int Created,
    int Updated,
    int TotalProcessed);

internal sealed class RegisterEmployeesCommandValidator : IValidator<RegisterEmployeesCommand>
{
    public IReadOnlyList<Error> Validate(RegisterEmployeesCommand instance)
        => string.IsNullOrWhiteSpace(instance.Payload.Content)
            ? new[] { Error.Validation("payload.empty", "요청 본문이 비어 있습니다. csv 또는 json 데이터를 전달해 주세요.") }
            : Array.Empty<Error>();
}
