using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Application.Employees.Dtos;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Domain.Employees;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Application.Employees.Commands.UpdateEmployee;

/// <summary>직원 한 명의 연락처를 전체 교체한다(PUT 시맨틱).</summary>
/// <remarks>
/// 일괄 업로드만으로는 "한 사람의 전화번호 한 자리"를 고치려고 파일 전체를 다시 올려야 한다.
/// 연락처는 개별 정정이 잦은 데이터라 단건 수정 경로가 필요하다.
/// </remarks>
public sealed record UpdateEmployeeCommand(
    int Id,
    string? Name,
    string? Email,
    string? Tel,
    string? Joined) : ICommand<EmployeeDto>;

internal sealed class UpdateEmployeeCommandValidator : IValidator<UpdateEmployeeCommand>
{
    public IReadOnlyList<Error> Validate(UpdateEmployeeCommand instance)
        => instance.Id <= 0
            ? new[] { Error.Validation("employee.id_invalid", "id 는 1 이상이어야 합니다.") }
            : Array.Empty<Error>();
}

internal sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateEmployeeCommandHandler> logger)
    : ICommandHandler<UpdateEmployeeCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> HandleAsync(
        UpdateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var employee = await repository.FindByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<EmployeeDto>(
                Error.NotFound("employee.not_found", $"id={command.Id} 직원을 찾을 수 없습니다."));
        }

        if (!JoinedDate.TryParse(command.Joined, out var joined))
        {
            return Result.Failure<EmployeeDto>(Error.Validation(
                "employee.joined_invalid",
                $"입사일 형식이 올바르지 않습니다: '{command.Joined}' (허용 형식: {string.Join(", ", JoinedDate.Formats)})"));
        }

        // 이메일은 자연 키다. 다른 사람이 이미 쓰고 있으면 DB 유일 제약에 걸리기 전에 409 로 알려준다.
        var normalizedEmail = EmailAddress.Create(command.Email);
        if (normalizedEmail.IsSuccess)
        {
            var taken = await repository
                .ExistsByEmailAsync(normalizedEmail.Value.Value, command.Id, cancellationToken)
                .ConfigureAwait(false);

            if (taken)
            {
                return Result.Failure<EmployeeDto>(Error.Conflict(
                    "employee.email_taken",
                    $"이메일 '{normalizedEmail.Value.Value}' 은(는) 다른 직원이 사용 중입니다."));
            }
        }

        var replaced = employee.Replace(
            command.Name,
            command.Email,
            command.Tel,
            joined,
            dateTimeProvider.Today);

        if (replaced.IsFailure)
        {
            return Result.Failure<EmployeeDto>(replaced.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("직원 {EmployeeId} 연락처 수정 완료", employee.Id);

        return Result.Success(new EmployeeDto(
            employee.Id,
            employee.Name,
            employee.Email.Value,
            employee.Tel.Formatted,
            employee.Joined,
            employee.CreatedAt,
            employee.UpdatedAt));
    }
}
