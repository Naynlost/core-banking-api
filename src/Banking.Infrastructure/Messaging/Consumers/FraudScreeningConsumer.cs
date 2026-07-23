using System.Text.Json;
using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.Events;
using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Banking.Infrastructure.Messaging.Consumers;

// Onaylanmış transferleri FraudPolicy'ye karşı tarar, eşleşen her kural için FraudAlert kaydeder
internal sealed class FraudScreeningConsumer(
    RabbitMqConnectionProvider connections,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<FraudScreeningConsumer> logger)
    : RabbitMqEventConsumer(connections, scopeFactory, timeProvider, logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<FraudScreeningConsumer> _logger = logger;

    protected override string ConsumerName => MessageTopology.FraudQueue;

    protected override string RoutingKey => MessageTopology.MoneyTransferredRoutingKey;

    protected override async Task HandleAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var transfer = JsonSerializer.Deserialize<MoneyTransferred>(payload)
            ?? throw new JsonException($"Empty {eventType} payload.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        // Pencere taranan transferde (dahil) biter, böylece tekrar teslimat da aynı sonuca ulaşır
        var transfersInWindow = await transactions.CountTransfersAsync(
            new AccountId(transfer.SourceAccountId),
            transfer.OccurredAt - FraudPolicy.VelocityWindow,
            transfer.OccurredAt.AddTicks(1),
            cancellationToken);

        var flags = FraudPolicy.Screen(transfer.Amount, transfer.CurrencyCode, transfersInWindow);
        if (flags.Count == 0)
        {
            _logger.LogInformation(
                "Fraud screening: transaction {TransactionId} clean ({Amount} {Currency})",
                transfer.TransactionId, transfer.Amount, transfer.CurrencyCode);
            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var flaggedAt = _timeProvider.GetUtcNow();
        var transactionId = new TransactionId(transfer.TransactionId);

        foreach (var flag in flags)
        {
            await context.FraudAlerts.AddAsync(FraudAlert.Raise(transactionId, flag, flaggedAt), cancellationToken);
            BankingDiagnostics.FraudAlerts.Add(1, new KeyValuePair<string, object?>("rule", flag.Rule));
            _logger.LogWarning(
                "Fraud screening: transaction {TransactionId} FLAGGED by {Rule}: {Detail}",
                transfer.TransactionId, flag.Rule, flag.Detail);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Bir önceki teslimat bu uyarıları zaten kaydetmiş, karar geçerli
        }
    }
}
