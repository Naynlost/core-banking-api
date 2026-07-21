using Banking.Application.Abstractions;
using Banking.Application.Tests.Fakes;
using Banking.Application.Transfers;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.Transfers;

// Handler'ın çapraz kur yolu: kuru alır, çevirir, bankanın pozisyonlarını hazırlar.
public class CrossCurrencyTransferHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly StagingIdempotencyStore _idempotency = new();
    private readonly InMemoryOutbox _outbox = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly Account _source;
    private readonly Account _destination;

    public CrossCurrencyTransferHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_idempotency);

        _source = Account.Open("user-1", Currency.Try).Value;
        _source.CompleteKyc();
        _destination = Account.Open("user-2", Currency.Usd).Value;
        _destination.CompleteKyc();

        _accounts.AddAsync(_source, CancellationToken.None);
        _accounts.AddAsync(_destination, CancellationToken.None);
        _transactions.SetTotals(_source.Id, debits: 0, credits: 10_000m);
    }

    // Bankanın USD pozisyonunu verilen stokla hazırlar
    private Account GivenUsdPosition(decimal stock)
    {
        var position = Account.OpenFxPosition(Currency.Usd);
        _accounts.AddAsync(position, CancellationToken.None);
        _transactions.SetTotals(position.Id, debits: 0, credits: stock);
        return position;
    }

    private TransferMoneyCommandHandler BuildHandler(decimal rate = 0.024m)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<ITransactionRepository>(_transactions);
        services.AddSingleton<IIdempotencyStore>(_idempotency);
        services.AddSingleton<IOutbox>(_outbox);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton<IExchangeRateProvider>(
            new FakeExchangeRateProvider(Currency.Try, Currency.Usd, rate));

        return new TransferMoneyCommandHandler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now));
    }

    private TransferMoneyCommand Command(decimal amount = 1_000m) =>
        new($"key-{Guid.NewGuid()}", "user-1", _source.Id.Value, _destination.Id.Value, amount, "TRY");

    [Fact]
    public async Task Handle_AcrossCurrencies_ConvertsAndPostsFourBalancedEntries()
    {
        var position = GivenUsdPosition(stock: 1_000m);

        var result = await BuildHandler(rate: 0.024m).HandleAsync(Command(1_000m), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error : string.Empty);
        var entries = _transactions.Added.ShouldHaveSingleItem().Entries;
        entries.Count.ShouldBe(4);

        // Gönderen 1.000 TRY verdi, alıcı 24 USD aldı (1.000 × 0,024)
        entries.ShouldContain(e =>
            e.AccountId == _source.Id && e.Direction == EntryDirection.Debit && e.Amount.Amount == 1_000m);
        entries.ShouldContain(e =>
            e.AccountId == _destination.Id && e.Direction == EntryDirection.Credit && e.Amount.Amount == 24m);
        entries.ShouldContain(e =>
            e.AccountId == position.Id && e.Direction == EntryDirection.Debit && e.Amount.Amount == 24m);
    }

    [Fact]
    public async Task Handle_AcrossCurrencies_BumpsThePositionVersionsToo()
    {
        var position = GivenUsdPosition(stock: 1_000m);
        var versionBefore = position.Version;

        await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        // Pozisyon hesapları da hareket gördü: eş zamanlı transferler onlarda da çakışmalı
        position.Version.ShouldBe(versionBefore + 1);
    }

    [Fact]
    public async Task Handle_WhenBankHasNoPositionInTheTargetCurrency_IsRejected()
    {
        // Hiç stok yüklenmemiş: banka satacak dövizi yok
        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFxLiquidity);
        _transactions.Added.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WhenPositionIsTooSmall_IsRejected()
    {
        GivenUsdPosition(stock: 23m); // 24 USD gerekiyor

        var result = await BuildHandler(rate: 0.024m).HandleAsync(Command(1_000m), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFxLiquidity);
        _transactions.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoRateIsAvailable_IsRejected()
    {
        GivenUsdPosition(stock: 1_000m);

        // Sağlayıcı yalnızca EUR→USD biliyor; TRY→USD sorulunca kur yok
        var services = new ServiceCollection();
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<ITransactionRepository>(_transactions);
        services.AddSingleton<IIdempotencyStore>(_idempotency);
        services.AddSingleton<IOutbox>(_outbox);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton<IExchangeRateProvider>(
            new FakeExchangeRateProvider(Currency.Eur, Currency.Usd, 1.1m));

        var handler = new TransferMoneyCommandHandler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ExchangeRateErrors.RateNotAvailable);
        _transactions.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AcrossCurrencies_StillEnforcesTheSenderBalance()
    {
        GivenUsdPosition(stock: 1_000m);
        _transactions.SetTotals(_source.Id, debits: 0, credits: 500m); // bakiye 500 TRY

        var result = await BuildHandler().HandleAsync(Command(1_000m), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
    }
}
