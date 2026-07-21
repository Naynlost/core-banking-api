using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Application.Transfers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banking.Infrastructure.Persistence;

public sealed class StandingOrderOptions
{
    public const string SectionName = "StandingOrders";

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(15);

    public int BatchSize { get; init; } = 20;
}

// Deterministik idempotency key'i sayesinde transfer-plan güncelleme arası çökme güvenli: tekrar çalıştırma
// aynı key'le zaten commit edilmiş sonucu döner, çift ödeme olmaz
internal sealed class StandingOrderExecutor(
    IServiceScopeFactory scopeFactory,
    IOptions<StandingOrderOptions> options,
    TimeProvider timeProvider,
    ILogger<StandingOrderExecutor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.Interval, stoppingToken);
                await ExecuteDueOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception, "Standing order pass failed; will retry in {Interval}", options.Value.Interval);
            }
        }
    }

    internal async Task<int> ExecuteDueOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var standingOrders = scope.ServiceProvider.GetRequiredService<IStandingOrderRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = timeProvider.GetUtcNow();
        var due = await standingOrders.GetDueAsync(now, options.Value.BatchSize, cancellationToken);

        foreach (var order in due)
        {
            // Key, RecordRun planı ilerletmeden ÖNCE okunur
            var command = new TransferMoneyCommand(
                order.CurrentRunKey,
                order.Owner,
                order.SourceAccountId.Value,
                order.DestinationAccountId.Value,
                order.Amount.Amount,
                order.Amount.Currency.Code);

            var result = await dispatcher.SendAsync(command, cancellationToken);
            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Standing order {StandingOrderId} executed as transaction {TransactionId}",
                    order.Id, result.Value);
            }
            else
            {
                logger.LogWarning(
                    "Standing order {StandingOrderId} occurrence failed: {Error}", order.Id, result.Error);
            }

            order.RecordRun(now, result.IsFailure ? result.Error : null);
        }

        if (due.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }
}
