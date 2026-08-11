using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Persistence;
using EmployeeDirectory.Application.Abstractions.Time;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Domain.Common;
using EmployeeDirectory.Domain.Employees;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Application.Employees.Commands.DeleteEmployee;

/// <summary>직원을 연락망에서 제외한다(soft delete).</summary>
public sealed record DeleteEmployeeCommand(int Id) : ICommand<Unit>;

internal sealed class DeleteEmployeeCommandValidator : IValidator<DeleteEmployeeCommand>
{
    public IReadOnlyList<Error> Validate(DeleteEmployeeCommand instance)
        => instance.Id <= 0
            ? new[] { Error.Validation("employee.id_invalid", "id 는 1 이상이어야 합니다.") }
            : Array.Empty<Error>();
}

internal sealed class DeleteEmployeeCommandHandler(
    IEmployeeRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeleteEmployeeCommandHandler> logger)
    : ICommandHandler<DeleteEmployeeCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await repository.FindByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<Unit>(
                Error.NotFound("employee.not_found", $"id={command.Id} 직원을 찾을 수 없습니다."));
        }

        employee.MarkDeleted(dateTimeProvider.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 누가 연락망에서 빠졌는지는 사후 추적이 필요한 사건이므로 남긴다.
        // 다만 로그는 보존 기간이 길고 접근 통제가 느슨하므로 이메일은 마스킹해서 남긴다.
        logger.LogInformation(
            "직원 {EmployeeId}({MaskedEmail}) 연락망에서 제외됨",
            employee.Id,
            employee.Email.Masked);

        return Result.Success(Unit.Value);
    }
}
