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
/// <param name="Restored">
/// 연락망에서 제외(soft delete)돼 있다가 복구된 건수.
/// 갱신과 합치지 않고 따로 세는 이유는, 빠져 있던 사람이 다시 살아나는 것은
/// 호출자가 <b>모르고 지나가면 안 되는</b> 변화이기 때문이다.
/// </param>
/// <param name="TotalProcessed">페이로드에서 읽어들인 총 건수(= Created + Updated + Restored).</param>
public sealed record RegisterEmployeesResult(
    EmployeeSourceFormat Format,
    int Created,
    int Updated,
    int Restored,
    int TotalProcessed);

internal sealed class RegisterEmployeesCommandValidator : IValidator<RegisterEmployeesCommand>
{
    public IReadOnlyList<Error> Validate(RegisterEmployeesCommand instance)
        => string.IsNullOrWhiteSpace(instance.Payload.Content)
            ? new[] { Error.Validation("payload.empty", "요청 본문이 비어 있습니다. csv 또는 json 데이터를 전달해 주세요.") }
            : Array.Empty<Error>();
}
