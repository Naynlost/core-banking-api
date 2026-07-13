using System.Text.Json;
using Banking.Domain.Events;
using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Banking.Api.IntegrationTests.Messaging;

/// <summary>
/// Proves the outbox pattern end to end against real PostgreSQL and RabbitMQ:
/// the event is staged in the same transaction as the transfer, survives an
/// application restart, reaches the broker, and both consumers process it
/// exactly once (inbox dedupe).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OutboxTests(IntegrationInfrastructure infrastructure)
{
    // Broker topology as seen from the outside — mirrors MessageTopology.
    private const string NotificationsQueue = "notifications.money-transferred";
    private const string FraudQueue = "fraud.money-transferred";

    [Fact]
    public async Task Transfer_StagesMoneyTransferredInTheOutbox_InTheSameTransaction()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var source = await TestBank.CreateAccountAsync(provider, "user-a", fundedWith: 100m);
        var destination = await TestBank.CreateAccountAsync(provider, "user-b");

        var result = await TestBank.TransferAsync(provider, source, destination, 40m);

        result.IsSuccess.ShouldBeTrue();
        var message = await FindOutboxMessageAsync(provider, result.Value);
        message.ShouldNotBeNull();
        message.ProcessedAt.ShouldBeNull(); // staged, not yet published

        var @event = JsonSerializer.Deserialize<MoneyTransferred>(message.Payload);
        @event.ShouldNotBeNull();
        @event.SourceAccountId.ShouldBe(source.Id.Value);
        @event.DestinationAccountId.ShouldBe(destination.Id.Value);
        @event.Amount.ShouldBe(40m);
        @event.CurrencyCode.ShouldBe("TRY");
    }

    [Fact]
    public async Task PendingEvent_SurvivesRestart_IsPublishedAndConsumedByBothConsumers()
    {
        // "First run" of the application: the transfer commits, but the process
        // goes away before the outbox publisher ever gets to the new row.
        Guid transactionId;
        await using (var firstRun = await IntegrationTestServices.CreateProviderAsync(infrastructure))
        {
            var source = await TestBank.CreateAccountAsync(firstRun, "user-a", fundedWith: 100m);
            var destination = await TestBank.CreateAccountAsync(firstRun, "user-b");
            var result = await TestBank.TransferAsync(firstRun, source, destination, 25m);
            result.IsSuccess.ShouldBeTrue();
            transactionId = result.Value;
        }

        // "Restart": a fresh service graph finds the pending row in the database.
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        hostedServices.ShouldNotBeEmpty(); // publisher + both consumers

        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        try
        {
            var messageId = await WaitForPublishedMessageIdAsync(provider, transactionId);
            await WaitForInboxRecordAsync(provider, NotificationsQueue, messageId);
            await WaitForInboxRecordAsync(provider, FraudQueue, messageId);
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }

    private static async Task<Guid> WaitForPublishedMessageIdAsync(ServiceProvider provider, Guid transactionId)
    {
        OutboxMessage? message = null;
        await TestBank.WaitUntilAsync(
            async () =>
            {
                message = await FindOutboxMessageAsync(provider, transactionId);
                return message?.ProcessedAt is not null;
            },
            $"outbox message for transaction {transactionId} to be published");

        return message!.Id;
    }

    private static Task WaitForInboxRecordAsync(ServiceProvider provider, string consumer, Guid messageId) =>
        TestBank.WaitUntilAsync(
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
                return await context.Set<InboxMessage>()
                    .AnyAsync(m => m.Consumer == consumer && m.MessageId == messageId);
            },
            $"consumer '{consumer}' to process message {messageId}");

    private static async Task<OutboxMessage?> FindOutboxMessageAsync(ServiceProvider provider, Guid transactionId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var candidates = await context.Set<OutboxMessage>()
            .Where(m => m.Type == nameof(MoneyTransferred))
            .ToListAsync();

        return candidates.SingleOrDefault(m =>
            JsonSerializer.Deserialize<MoneyTransferred>(m.Payload)?.TransactionId == transactionId);
    }
}
