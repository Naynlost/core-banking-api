using System.Diagnostics;
using System.Text.Json;
using Banking.Application.Abstractions;
using Banking.Infrastructure.Persistence;

namespace Banking.Infrastructure.Messaging;

internal sealed class Outbox(BankingDbContext context, TimeProvider timeProvider) : IOutbox
{
    public async Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class =>
        await context.Set<OutboxMessage>().AddAsync(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = @event.GetType().Name,
                Payload = JsonSerializer.Serialize(@event, @event.GetType()),
                OccurredAt = timeProvider.GetUtcNow(),
                // Çağıran trace'i burada yakala; yayın çok sonra background service'te olur
                TraceParent = Activity.Current?.Id,
                CorrelationId = Activity.Current?.GetBaggageItem(MessagingDiagnostics.CorrelationBaggageKey),
            },
            cancellationToken);
}
