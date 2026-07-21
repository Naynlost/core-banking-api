using Banking.Application.Abstractions;
using Banking.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banking.Infrastructure.Persistence;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    public int MessagingDays { get; init; } = 7;

    // Bu sürenin sonunda aynı key tekrar çalışabilir, gerçekçi client retry'sini rahatça aşmalı
    public int IdempotencyHours { get; init; } = 24;

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
}

// Outbox/inbox/idempotency tabloları sadece büyür; süresi geçmiş satırları periyodik siler. Bekleyen outbox asla silinmez.
internal sealed class RetentionCleaner(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<RetentionCleaner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.Interval, stoppingToken);
                await CleanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception, "Retention pass failed; will retry in {Interval}", options.Value.Interval);
            }
        }
    }

    internal async Task<int> CleanOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var now = timeProvider.GetUtcNow();
        var messagingCutoff = now.AddDays(-options.Value.MessagingDays);
        var idempotencyCutoff = now.AddHours(-options.Value.IdempotencyHours);

        var removed = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < messagingCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        removed += await context.Set<InboxMessage>()
            .Where(m => m.ProcessedAt < messagingCutoff)
            .ExecuteDeleteAsync(cancellationToken);
        removed += await context.Set<IdempotencyRecord>()
            .Where(r => r.CreatedAt < idempotencyCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            logger.LogInformation("Retention pass removed {Count} expired rows", removed);
        }

        return removed;
    }
}
