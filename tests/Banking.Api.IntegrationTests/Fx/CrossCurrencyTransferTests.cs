using Banking.Application.Abstractions;
using Banking.Application.Fx;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Banking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Fx;

// Gerçek PostgreSQL üzerinde çapraz kur transferi: hazine stok yükler, müşteri TRY gönderir,
// karşı taraf USD alır ve defter HER İKİ para biriminde ayrı ayrı dengede kalır.
[Collection(IntegrationCollection.Name)]
public sealed class CrossCurrencyTransferTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync() =>
        _provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task CrossCurrencyTransfer_ConvertsAndKeepsEveryCurrencyBalanced()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "fx-user-a", fundedWith: 5_000m);
        var destination = await TestBank.CreateAccountAsync(
            _provider, "fx-user-b", currency: Currency.Usd);

        // Pozisyonlar bankanın tamamı için tektir ve aynı veritabanını paylaşan diğer
        // testlerden etkilenir; bu yüzden mutlak değil, fark üzerinden doğruluyoruz.
        var usdBefore = await PositionBalanceAsync(Currency.Usd);
        var tryBefore = await PositionBalanceAsync(Currency.Try);

        // Banka önce USD stoğu yüklemeli, yoksa satacak dövizi yok
        await FundPositionAsync(1_000m, "USD");

        // 1 USD = 40 TRY olduğundan 2.000 TRY tam 50 USD eder
        var transfer = await TestBank.TransferAsync(_provider, source, destination, 2_000m);
        transfer.IsSuccess.ShouldBeTrue(transfer.IsFailure ? transfer.Error : string.Empty);

        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(3_000m);
        (await TestBank.GetBalanceAsync(_provider, destination)).ShouldBe(50m);

        await using var scope = _provider.CreateAsyncScope();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var posted = (await transactions.GetByIdAsync(
            new TransactionId(transfer.Value), CancellationToken.None)).ShouldNotBeNull();

        // Dört satır: TRY bacağı ve USD bacağı, her biri kendi içinde sıfırlanıyor
        posted.Entries.Count.ShouldBe(4);
        foreach (var currency in new[] { Currency.Try, Currency.Usd })
        {
            var debits = posted.Entries
                .Where(e => e.Amount.Currency == currency && e.Direction == EntryDirection.Debit)
                .Sum(e => e.Amount.Amount);
            var credits = posted.Entries
                .Where(e => e.Amount.Currency == currency && e.Direction == EntryDirection.Credit)
                .Sum(e => e.Amount.Amount);

            debits.ShouldBe(credits, $"{currency} bacağı defterde dengeli olmalı");
        }

        // Banka 1.000 USD stok aldı, 50 USD ödedi; karşılığında 2.000 TRY topladı
        (await PositionBalanceAsync(Currency.Usd)).ShouldBe(usdBefore + 1_000m - 50m);
        (await PositionBalanceAsync(Currency.Try)).ShouldBe(tryBefore + 2_000m);
    }

    [Fact]
    public async Task CrossCurrencyTransfer_WithoutBankLiquidity_IsRejected()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "fx-user-c", fundedWith: 5_000m);
        var destination = await TestBank.CreateAccountAsync(
            _provider, "fx-user-d", currency: Currency.Eur);

        // EUR pozisyonuna hiç stok yüklenmedi
        var transfer = await TestBank.TransferAsync(_provider, source, destination, 1_000m);

        transfer.IsFailure.ShouldBeTrue();
        transfer.Error.ShouldBe(LedgerErrors.InsufficientFxLiquidity);
        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(5_000m);
        (await TestBank.GetBalanceAsync(_provider, destination)).ShouldBe(0m);
    }

    [Fact]
    public async Task FundingTheSamePositionTwiceWithOneKey_AppliesOnce()
    {
        var key = $"fx-fund-{Guid.NewGuid():N}";
        var before = await PositionBalanceAsync(Currency.Usd);

        var first = await SendFundingAsync(key, 500m, "USD");
        var second = await SendFundingAsync(key, 500m, "USD");

        first.IsSuccess.ShouldBeTrue(first.IsFailure ? first.Error : string.Empty);
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(first.Value);

        // Tek yükleme uygulandı: stok 1.000 değil 500 arttı
        (await PositionBalanceAsync(Currency.Usd)).ShouldBe(before + 500m);
    }

    [Fact]
    public async Task Quote_ReturnsTheRateAndConvertedAmount()
    {
        await using var scope = _provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var quote = await dispatcher.QueryAsync(
            new GetFxQuoteQuery("TRY", "USD", 2_000m), CancellationToken.None);

        quote.IsSuccess.ShouldBeTrue();
        quote.Value.ConvertedAmount.ShouldBe(50m);
        quote.Value.From.ShouldBe("TRY");
        quote.Value.To.ShouldBe("USD");
    }

    private Task FundPositionAsync(decimal amount, string currencyCode) =>
        SendFundingAsync($"fx-fund-{Guid.NewGuid():N}", amount, currencyCode);

    private async Task<Domain.Primitives.Result<Guid>> SendFundingAsync(
        string key, decimal amount, string currencyCode)
    {
        await using var scope = _provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        return await dispatcher.SendAsync(
            new FundFxPositionCommand(key, "treasury-user", amount, currencyCode), CancellationToken.None);
    }

    private async Task<decimal> PositionBalanceAsync(Currency currency)
    {
        await using var scope = _provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var position = await accounts.GetFxPositionAccountAsync(currency, CancellationToken.None);
        if (position is null)
        {
            return 0m;
        }

        var totals = await transactions.GetEntryTotalsAsync(position.Id, CancellationToken.None);
        return LedgerMath.Balance(position, totals.Debits, totals.Credits).Amount;
    }
}
