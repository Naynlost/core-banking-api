using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Application.StandingOrders;
using Banking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Banking.Api.IntegrationTests.StandingOrders;

// Vadesi gelen emir normal transfer olarak çalışır; deterministik key sayesinde crash-and-rerun çift ödemez
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

        // Hemen vadesi gelen aylık bir emir
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

        // Planı veritabanından geri oku: Postgres mikrosaniyeye yuvarlar, key kaydedilenden hesaplanmalı
        await using (var scope = provider.CreateAsyncScope())
        {
            var order = (await scope.ServiceProvider.GetRequiredService<IStandingOrderRepository>()
                .GetByIdAsync(orderId, CancellationToken.None)).ShouldNotBeNull();
            firstOccurrence = order.NextRunAt;
        }

        // İlk tur vadesi geleni çalıştırır ve planı ilerletir
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(1);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m);

        await using (var scope = provider.CreateAsyncScope())
        {
            var order = (await scope.ServiceProvider.GetRequiredService<IStandingOrderRepository>()
                .GetByIdAsync(orderId, CancellationToken.None)).ShouldNotBeNull();
            order.LastRunError.ShouldBeNull();
            order.NextRunAt.ShouldBe(firstOccurrence.AddMonths(1));
        }

        // Başka vadesi gelen olmadığından ikinci tur para taşımaz
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(0);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m);

        // AYNI occurrence'da crash-and-rerun: executor transferden sonra ama RecordRun'dan önce ölmüş gibi
        // planı geri sar. Key deterministik olduğundan tekrar çalıştırma para ödemez, commit edilmiş sonucu döner.
        await RewindNextRunAsync(provider, orderId, firstOccurrence);
        (await executor.ExecuteDueOnceAsync(CancellationToken.None)).ShouldBe(1);
        (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(250m); // hâlâ tam olarak bir kez

        // İptal edilmiş emir bir daha asla alınmaz
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
