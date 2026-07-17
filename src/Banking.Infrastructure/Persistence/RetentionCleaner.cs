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

    /// <summary>How long published outbox rows and consumer inbox rows are kept.</summary>
    public int MessagingDays { get; init; } = 7;

    /// <summary>
    /// How long idempotency keys are kept. After this window the same key would
    /// execute again, so it must comfortably exceed any realistic client retry.
    /// </summary>
    public int IdempotencyHours { get; init; } = 24;

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
}

/// <summary>
/// The outbox, inbox and idempotency tables only ever grow; this service
/// periodically deletes rows that no longer serve their purpose (published
/// events, old dedupe marks, expired idempotency keys). Unpublished outbox rows
/// are never touched.
/// </summary>
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
