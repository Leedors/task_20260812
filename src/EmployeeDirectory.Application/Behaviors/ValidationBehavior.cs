using EmployeeDirectory.Application.Abstractions.Messaging;
using EmployeeDirectory.Application.Abstractions.Validation;
using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Behaviors;

/// <summary>
/// 핸들러 실행 전에 등록된 모든 <see cref="IValidator{T}"/> 를 돌려 실패를 모아 반환한다.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var errors = validators.SelectMany(validator => validator.Validate(request)).ToArray();

        return errors.Length > 0
            ? Task.FromResult(Result.Failure<TResponse>(errors))
            : next();
    }
}
