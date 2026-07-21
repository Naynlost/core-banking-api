namespace Banking.Application.Abstractions;

// Olay, işlemle AYNI transaction'da yazılır (outbox pattern); publisher sonradan broker'a gönderir
public interface IOutbox
{
    Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class;
}
