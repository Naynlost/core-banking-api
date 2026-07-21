using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.Ledgers;

// Çapraz kur transferi: TRY bacağı ve USD bacağı kendi içlerinde dengelenir,
// arada bankanın döviz pozisyonları durur.
public class CrossCurrencyTransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);

    private static Money Try(decimal amount) => Money.Create(amount, Currency.Try).Value;

    private static Money Usd(decimal amount) => Money.Create(amount, Currency.Usd).Value;

    private static Account Customer(Currency currency)
    {
        var account = Account.Open("musteri", currency).Value;
        account.CompleteKyc();
        return account;
    }

    private static Result<Transaction> Transfer(
        decimal amount = 1_000m,
        decimal converted = 24m,
        decimal positionBalance = 1_000m,
        Account? sourcePosition = null,
        Account? destinationPosition = null)
    {
        var source = Customer(Currency.Try);
        var destination = Customer(Currency.Usd);

        var fx = new FxContext(
            Usd(converted),
            sourcePosition ?? Account.OpenFxPosition(Currency.Try),
            destinationPosition ?? Account.OpenFxPosition(Currency.Usd),
            Usd(positionBalance));

        return TransferPolicy.Transfer(
            source, Try(10_000m), Try(0m), destination, Try(amount), Now, fx);
    }

    [Fact]
    public void Transfer_AcrossCurrencies_BalancesEachCurrencySeparately()
    {
        var result = Transfer(amount: 1_000m, converted: 24m);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error : string.Empty);
        var entries = result.Value.Entries;
        entries.Count.ShouldBe(4);

        foreach (var currency in new[] { Currency.Try, Currency.Usd })
        {
            var debits = entries
                .Where(e => e.Amount.Currency == currency && e.Direction == EntryDirection.Debit)
                .Sum(e => e.Amount.Amount);
            var credits = entries
                .Where(e => e.Amount.Currency == currency && e.Direction == EntryDirection.Credit)
                .Sum(e => e.Amount.Amount);

            debits.ShouldBe(credits, $"{currency} bacağı dengeli olmalı");
        }
    }

    [Fact]
    public void Transfer_AcrossCurrencies_MovesMoneyThroughTheBankPositions()
    {
        var sourcePosition = Account.OpenFxPosition(Currency.Try);
        var destinationPosition = Account.OpenFxPosition(Currency.Usd);

        var result = Transfer(
            amount: 1_000m,
            converted: 24m,
            sourcePosition: sourcePosition,
            destinationPosition: destinationPosition);

        var entries = result.Value.Entries;

        // Gönderilen TRY bankanın TRY pozisyonuna girer
        entries.ShouldContain(e =>
            e.AccountId == sourcePosition.Id
            && e.Direction == EntryDirection.Credit
            && e.Amount.Amount == 1_000m);

        // Ödenen USD bankanın USD pozisyonundan çıkar
        entries.ShouldContain(e =>
            e.AccountId == destinationPosition.Id
            && e.Direction == EntryDirection.Debit
            && e.Amount.Amount == 24m);
    }

    [Fact]
    public void Transfer_WhenBankHasTooLittleOfTheTargetCurrency_IsRejected()
    {
        var result = Transfer(converted: 24m, positionBalance: 23.99m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFxLiquidity);
    }

    [Fact]
    public void Transfer_WhenBankHasExactlyEnough_Succeeds()
    {
        var result = Transfer(converted: 24m, positionBalance: 24m);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Transfer_WithAnAccountThatIsNotAnFxPosition_IsRejected()
    {
        var result = Transfer(destinationPosition: Account.OpenCash(Currency.Usd));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.NotAnFxPosition);
    }

    [Fact]
    public void Transfer_StillEnforcesTheSendersRules()
    {
        // Çapraz kur yolu, gönderenin kurallarını atlamaz: KYC'siz hesap gönderemez
        var source = Account.Open("musteri", Currency.Try).Value;
        var destination = Customer(Currency.Usd);
        var fx = new FxContext(
            Usd(24m),
            Account.OpenFxPosition(Currency.Try),
            Account.OpenFxPosition(Currency.Usd),
            Usd(1_000m));

        var result = TransferPolicy.Transfer(
            source, Try(10_000m), Try(0m), destination, Try(1_000m), Now, fx);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycNotVerified);
    }

    [Fact]
    public void Transfer_WithoutFxContext_StillRejectsMismatchedCurrencies()
    {
        // Kur bağlamı verilmezse eski davranış korunur: farklı birimler transfer edilemez
        var source = Customer(Currency.Try);
        var destination = Customer(Currency.Usd);

        var result = TransferPolicy.Transfer(
            source, Try(10_000m), Try(0m), destination, Try(100m), Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.CurrencyMismatch);
    }
}
