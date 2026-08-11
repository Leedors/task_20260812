using EmployeeDirectory.Domain.Common;

namespace EmployeeDirectory.Application.Abstractions.Messaging;

/// <summary>커맨드를 해당 핸들러로 보낸다.</summary>
public interface ICommandDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
}

/// <summary>쿼리를 해당 핸들러로 보낸다.</summary>
public interface IQueryDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
