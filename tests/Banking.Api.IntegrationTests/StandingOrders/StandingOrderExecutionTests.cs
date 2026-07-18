using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Application.StandingOrders;
using Banking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Banking.Api.IntegrationTests.StandingOrders;

/// <summary>
/// The standing order executor against real PostgreSQL: a due order executes as
/// a regular ledger transfer, the schedule advances, and — because every
/// occurrence carries a deterministic idempotency key — a repeated pass over
/// the same occurrence (crash-and-rerun scenario) never moves the money twice.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class StandingOrderExecutionTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task DueOrder_ExecutesExactlyOnce_AdvancesSchedule_AndStopsWhenCancelled()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var source = await TestBank.CreateAccountAsync(provider, "so-user-a", fundedWith: 1_000m);
        var destination = await TestBank.CreateAccountAsync(provider, "so-user-b");

        var executor = new StandingOrderExecutor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new StandingOrderOptions()),
            TimeProvider.System,
            NullLogger<StandingOrderExecutor>.Instance);

        // A monthly order due immediately.
        Guid orderId;
        DateTimeOffset firstOccurrence;
        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var created = await dispatcher.SendAsync(
                new CreateStandingOrderCommand(
                    "so-user-a", source.Id.Value, destination.Id.Value, 250m, "TRY", "Monthly"),
                CancellationToken.None);
            created.IsSuccess.ShouldBeTrue(created.IsFailure ? created.Error : string.Empty);
            orderId = created.Value;
        }

        // Read the schedule back from the database: PostgreSQL rounds to
        // microseconds, and the run key must be computed from what is stored.
        await using (var scope = provider.CreateAsyncScope())
        {
            var order = (await scope.ServiceProvider.GetRequiredService<IStandingOrderRepository>()
                .GetByIdAsync(orderId, CancellationToken.None)).ShouldNotBeNull();
            firstOccurrence = order.NextRunAt;
        }

        // First pass executes the due occurrence and advances the schedule.
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(1);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m);

        await using (var scope = provider.CreateAsyncScope())
        {
            var order = (await scope.ServiceProvider.GetRequiredService<IStandingOrderRepository>()
                .GetByIdAsync(orderId, CancellationToken.None)).ShouldNotBeNull();
            order.LastRunError.ShouldBeNull();
            order.NextRunAt.ShouldBe(firstOccurrence.AddMonths(1));
        }

        // Nothing further is due, so a second pass moves no money.
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(0);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m);

        // Crash-and-rerun on the SAME occurrence: rewind the schedule to the
        // exact time already executed, as if the executor died after the
        // transfer but before saving RecordRun. The occurrence key is
        // deterministic, so the replay returns the committed transaction
        // instead of paying again.
        await RewindNextRunAsync(provider, orderId, firstOccurrence);
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(1);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m); // still exactly once

        // A cancelled order is never picked up again, due or not.
        await RewindNextRunAsync(provider, orderId, firstOccurrence);
        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            (await dispatcher.SendAsync(
                new CancelStandingOrderCommand(orderId, "so-user-a"), CancellationToken.None))
                .IsSuccess.ShouldBeTrue();
        }

        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(0);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m);
    }

    private static async Task RewindNextRunAsync(
        IServiceProvider provider, Guid orderId, DateTimeOffset nextRunAt)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var order = (await context.StandingOrders.FindAsync(orderId)).ShouldNotBeNull();
        context.Entry(order).Property(o => o.NextRunAt).CurrentValue = nextRunAt;
        await context.SaveChangesAsync();
    }
}
