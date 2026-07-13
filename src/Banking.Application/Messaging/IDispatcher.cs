using Banking.Domain.Primitives;

namespace Banking.Application.Messaging;

/// <summary>
/// Routes commands and queries to their single registered handler.
/// The application's in-process replacement for a mediator library.
/// </summary>
public interface IDispatcher
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);

    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
