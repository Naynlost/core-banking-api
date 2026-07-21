using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.Ledgers;

public class TransactionTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    private static Money Try(decimal amount) => Money.Create(amount, Currency.Try).Value;

    [Fact]
    public void Create_WithSingleEntry_Fails()
    {
        var result = Transaction.Create("Test", Timestamp,
            [new EntrySpec(AccountId.New(), Try(100m), EntryDirection.Debit)]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.AtLeastTwoEntries);
    }

    [Fact]
    public void Create_WhenDebitsAndCreditsDiffer_Fails()
    {
        var result = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(AccountId.New(), Try(100m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Try(90m), EntryDirection.Credit),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.Unbalanced);
    }

    [Fact]
    public void Create_WithZeroAmountEntry_Fails()
    {
        var result = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(AccountId.New(), Try(0m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Try(0m), EntryDirection.Credit),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.EntryAmountMustBePositive);
    }

    [Fact]
    public void Create_WhenOneCurrencyDoesNotBalanceAgainstAnother_Fails()
    {
        // Farklı para birimleri birbirini dengeleyemez: 100 TRY borç, 100 USD alacak
        // yazmak iki bacağı da açıkta bırakır.
        var result = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(AccountId.New(), Try(100m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Money.Create(100m, Currency.Usd).Value, EntryDirection.Credit),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.Unbalanced);
    }

    [Fact]
    public void Create_WithMultipleCurrenciesEachBalanced_Succeeds()
    {
        // Çapraz kur işleminin şekli: TRY bacağı kendi içinde, USD bacağı kendi içinde sıfırlanır.
        var usd = Money.Create(30m, Currency.Usd).Value;

        var result = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(AccountId.New(), Try(1_000m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Try(1_000m), EntryDirection.Credit),
            new EntrySpec(AccountId.New(), usd, EntryDirection.Debit),
            new EntrySpec(AccountId.New(), usd, EntryDirection.Credit),
        ]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Entries.Count.ShouldBe(4);
    }

    [Fact]
    public void Create_WithBalancedEntries_Succeeds()
    {
        var debited = AccountId.New();
        var credited = AccountId.New();

        var result = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(debited, Try(100m), EntryDirection.Debit),
            new EntrySpec(credited, Try(100m), EntryDirection.Credit),
        ]);

        result.IsSuccess.ShouldBeTrue();
        var transaction = result.Value;
        transaction.Entries.Count.ShouldBe(2);
        transaction.Entries.ShouldAllBe(e => e.TransactionId == transaction.Id);
        transaction.Entries.ShouldContain(e => e.AccountId == debited && e.Direction == EntryDirection.Debit);
        transaction.Entries.ShouldContain(e => e.AccountId == credited && e.Direction == EntryDirection.Credit);
    }

    [Fact]
    public void Create_WithBalancedEntries_SignedSumIsZero()
    {
        var transaction = Transaction.Create("Test", Timestamp,
        [
            new EntrySpec(AccountId.New(), Try(60m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Try(40m), EntryDirection.Debit),
            new EntrySpec(AccountId.New(), Try(100m), EntryDirection.Credit),
        ]).Value;

        var signedSum = transaction.Entries.Sum(e =>
            e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount);

        signedSum.ShouldBe(0m);
    }
}
