using Banking.Domain.Primitives;

namespace Banking.Application.Messaging;

// Mediator kütüphanesi yerine kullanılan, süreç içi kendi dispatcher'ımız
public interface IDispatcher
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);

    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
