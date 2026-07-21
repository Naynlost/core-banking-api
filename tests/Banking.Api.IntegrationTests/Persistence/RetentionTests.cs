using Banking.Application.Abstractions;
using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Banking.Api.IntegrationTests.Persistence;

// Retention geçişi sadece işi biteni siler: yayınlanmış outbox, eski inbox, süresi dolmuş idempotency key
[Collection(IntegrationCollection.Name)]
public sealed class RetentionTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task CleanOnce_RemovesExpiredRowsAndKeepsLiveOnes()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var now = DateTimeOffset.UtcNow;

        var oldPublished = NewOutboxMessage(now.AddDays(-10), processedAt: now.AddDays(-9));
        var oldPending = NewOutboxMessage(now.AddDays(-10), processedAt: null); // hiç yayınlanmadı: silinmemeli
        var freshPublished = NewOutboxMessage(now.AddHours(-1), processedAt: now.AddMinutes(-30));
        var oldIdempotencyKey = $"old-{Guid.NewGuid():N}";
        var freshIdempotencyKey = $"fresh-{Guid.NewGuid():N}";

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
            context.AddRange(oldPublished, oldPending, freshPublished);
            context.Add(new InboxMessage
            {
                Consumer = "retention-test",
                MessageId = Guid.NewGuid(),
                ProcessedAt = now.AddDays(-9),
            });
            context.Add(new IdempotencyRecord(oldIdempotencyKey, "user-r", Guid.NewGuid(), now.AddHours(-25)));
            context.Add(new IdempotencyRecord(freshIdempotencyKey, "user-r", Guid.NewGuid(), now.AddHours(-1)));
            await context.SaveChangesAsync();
        }

        var cleaner = new RetentionCleaner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RetentionOptions()),
            TimeProvider.System,
            NullLogger<RetentionCleaner>.Instance);

        var removed = await cleaner.CleanOnceAsync(CancellationToken.None);

        removed.ShouldBeGreaterThanOrEqualTo(3); // eski outbox + eski inbox + eski idempotency
        await using var verify = provider.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<BankingDbContext>();
        (await db.Set<OutboxMessage>().AnyAsync(m => m.Id == oldPublished.Id)).ShouldBeFalse();
        (await db.Set<OutboxMessage>().AnyAsync(m => m.Id == oldPending.Id)).ShouldBeTrue();
        (await db.Set<OutboxMessage>().AnyAsync(m => m.Id == freshPublished.Id)).ShouldBeTrue();
        (await db.Set<InboxMessage>().AnyAsync(m => m.Consumer == "retention-test")).ShouldBeFalse();
        (await db.Set<IdempotencyRecord>().AnyAsync(r => r.Key == oldIdempotencyKey)).ShouldBeFalse();
        (await db.Set<IdempotencyRecord>().AnyAsync(r => r.Key == freshIdempotencyKey)).ShouldBeTrue();
    }

    private static OutboxMessage NewOutboxMessage(DateTimeOffset occurredAt, DateTimeOffset? processedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = "RetentionProbe",
            Payload = "{}",
            OccurredAt = occurredAt,
            ProcessedAt = processedAt,
        };
}
