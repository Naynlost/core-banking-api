using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.Ledgers;

public class LedgerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    private static Money Try(decimal amount) => Money.Create(amount, Currency.Try).Value;

    private static Account OpenAccount()
    {
        var account = Account.Open("Ayşe Yılmaz", Currency.Try).Value;
        account.CompleteKyc();
        return account;
    }

    [Fact]
    public void GetBalance_ForNewAccount_IsZero()
    {
        var ledger = new Ledger();

        ledger.GetBalance(OpenAccount()).ShouldBe(Money.Zero(Currency.Try));
    }

    [Fact]
    public void Deposit_IncreasesBalance()
    {
        var ledger = new Ledger();
        var account = OpenAccount();

        var result = ledger.Deposit(account, Try(100m), Timestamp);

        result.IsSuccess.ShouldBeTrue();
        ledger.GetBalance(account).ShouldBe(Try(100m));
    }

    [Fact]
    public void Deposit_PostsBalancedDoubleEntryTransaction()
    {
        var ledger = new Ledger();
        var account = OpenAccount();

        var transaction = ledger.Deposit(account, Try(100m), Timestamp).Value;

        transaction.Entries.Count.ShouldBe(2);
        transaction.Entries.ShouldContain(e =>
            e.AccountId == account.Id && e.Direction == EntryDirection.Credit);
        transaction.Entries.ShouldContain(e =>
            e.AccountId == ledger.CashAccount(Currency.Try).Id && e.Direction == EntryDirection.Debit);
    }

    [Fact]
    public void Withdraw_ReducesBalance()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        ledger.Deposit(account, Try(100m), Timestamp);

        var result = ledger.Withdraw(account, Try(30m), Timestamp);

        result.IsSuccess.ShouldBeTrue();
        ledger.GetBalance(account).ShouldBe(Try(70m));
    }

    [Fact]
    public void Withdraw_MoreThanBalance_FailsAndLeavesLedgerUntouched()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        ledger.Deposit(account, Try(100m), Timestamp);

        var result = ledger.Withdraw(account, Try(100.01m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
        ledger.GetBalance(account).ShouldBe(Try(100m));
        ledger.Transactions.Count.ShouldBe(1);
    }

    [Fact]
    public void Withdraw_FromEmptyAccount_Fails()
    {
        var ledger = new Ledger();

        var result = ledger.Withdraw(OpenAccount(), Try(1m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
    }

    [Fact]
    public void Deposit_ToClosedAccount_Fails()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        account.Close();

        var result = ledger.Deposit(account, Try(100m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
        ledger.Transactions.ShouldBeEmpty();
    }

    [Fact]
    public void Withdraw_FromClosedAccount_Fails()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        ledger.Deposit(account, Try(100m), Timestamp);
        account.Close();

        var result = ledger.Withdraw(account, Try(30m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
        ledger.GetBalance(account).ShouldBe(Try(100m));
    }

    [Fact]
    public void Deposit_WithMismatchedCurrency_Fails()
    {
        var ledger = new Ledger();

        var result = ledger.Deposit(OpenAccount(), Money.Create(100m, Currency.Usd).Value, Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.CurrencyMismatch);
    }

    [Fact]
    public void Deposit_WithZeroAmount_Fails()
    {
        var ledger = new Ledger();

        var result = ledger.Deposit(OpenAccount(), Money.Zero(Currency.Try), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.AmountMustBePositive);
    }

    [Fact]
    public void Transfer_MovesMoneyBetweenAccounts()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        var result = ledger.Transfer(source, destination, Try(40m), Timestamp);

        result.IsSuccess.ShouldBeTrue();
        ledger.GetBalance(source).ShouldBe(Try(60m));
        ledger.GetBalance(destination).ShouldBe(Try(40m));
    }

    [Fact]
    public void Transfer_PostsBalancedDoubleEntryTransaction()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        var transaction = ledger.Transfer(source, destination, Try(40m), Timestamp).Value;

        transaction.Entries.Count.ShouldBe(2);
        transaction.Entries.ShouldContain(e =>
            e.AccountId == source.Id && e.Direction == EntryDirection.Debit);
        transaction.Entries.ShouldContain(e =>
            e.AccountId == destination.Id && e.Direction == EntryDirection.Credit);
    }

    [Fact]
    public void Transfer_DoesNotChangeCashBalance()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        ledger.Transfer(source, destination, Try(40m), Timestamp);

        ledger.GetBalance(ledger.CashAccount(Currency.Try)).ShouldBe(Try(100m));
    }

    [Fact]
    public void Transfer_MoreThanBalance_FailsAndLeavesLedgerUntouched()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        var result = ledger.Transfer(source, destination, Try(100.01m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
        ledger.GetBalance(source).ShouldBe(Try(100m));
        ledger.GetBalance(destination).ShouldBe(Try(0m));
        ledger.Transactions.Count.ShouldBe(1);
    }

    [Fact]
    public void Transfer_ToSameAccount_Fails()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        ledger.Deposit(account, Try(100m), Timestamp);

        var result = ledger.Transfer(account, account, Try(10m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.SameAccount);
    }

    [Fact]
    public void Transfer_FromClosedAccount_Fails()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);
        source.Close();

        var result = ledger.Transfer(source, destination, Try(10m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
    }

    [Fact]
    public void Transfer_ToClosedAccount_Fails()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);
        destination.Close();

        var result = ledger.Transfer(source, destination, Try(10m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
    }

    [Fact]
    public void Transfer_WithZeroAmount_Fails()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        var result = ledger.Transfer(source, destination, Money.Zero(Currency.Try), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.AmountMustBePositive);
    }

    [Fact]
    public void Transfer_FromKycPendingAccount_Fails()
    {
        var ledger = new Ledger();
        var source = Account.Open("Ayşe Yılmaz", Currency.Try).Value; // KYC still pending
        var destination = OpenAccount();
        ledger.Deposit(source, Try(100m), Timestamp);

        var result = ledger.Transfer(source, destination, Try(10m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycNotVerified);
        ledger.GetBalance(source).ShouldBe(Try(100m));
    }

    [Fact]
    public void Transfer_ToKycPendingAccount_Succeeds()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = Account.Open("Mehmet Demir", Currency.Try).Value; // receiving needs no KYC
        ledger.Deposit(source, Try(100m), Timestamp);

        var result = ledger.Transfer(source, destination, Try(40m), Timestamp);

        result.IsSuccess.ShouldBeTrue();
        ledger.GetBalance(destination).ShouldBe(Try(40m));
    }

    [Fact]
    public void Transfer_UpToTheDailyLimit_Succeeds()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(30_000m), Timestamp); // deposits don't consume the limit

        ledger.Transfer(source, destination, Try(15_000m), Timestamp).IsSuccess.ShouldBeTrue();
        ledger.Transfer(source, destination, Try(5_000m), Timestamp).IsSuccess.ShouldBeTrue(); // exactly at the limit

        ledger.GetBalance(source).ShouldBe(Try(10_000m));
    }

    [Fact]
    public void Transfer_ExceedingTheDailyLimit_FailsAndLeavesLedgerUntouched()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(30_000m), Timestamp);
        ledger.Transfer(source, destination, Try(15_000m), Timestamp);

        var result = ledger.Transfer(source, destination, Try(6_000m), Timestamp);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.DailyLimitExceeded);
        ledger.GetBalance(source).ShouldBe(Try(15_000m));
        ledger.Transactions.Count.ShouldBe(2); // deposit + first transfer only
    }

    [Fact]
    public void Transfer_DailyLimit_ResetsOnTheNextDay()
    {
        var ledger = new Ledger();
        var source = OpenAccount();
        var destination = OpenAccount();
        ledger.Deposit(source, Try(40_000m), Timestamp);
        ledger.Transfer(source, destination, Try(15_000m), Timestamp).IsSuccess.ShouldBeTrue();

        var nextDay = ledger.Transfer(source, destination, Try(15_000m), Timestamp.AddDays(1));

        nextDay.IsSuccess.ShouldBeTrue();
        ledger.GetBalance(source).ShouldBe(Try(10_000m));
    }

    [Fact]
    public void CashBalance_EqualsSumOfCustomerBalances()
    {
        var ledger = new Ledger();
        var first = OpenAccount();
        var second = OpenAccount();

        ledger.Deposit(first, Try(100m), Timestamp);
        ledger.Deposit(second, Try(50m), Timestamp);
        ledger.Withdraw(first, Try(20m), Timestamp);

        ledger.GetBalance(first).ShouldBe(Try(80m));
        ledger.GetBalance(second).ShouldBe(Try(50m));
        ledger.GetBalance(ledger.CashAccount(Currency.Try)).ShouldBe(Try(130m));
    }

    [Fact]
    public void EveryPostedTransaction_HasZeroSignedSum()
    {
        var ledger = new Ledger();
        var account = OpenAccount();
        ledger.Deposit(account, Try(100m), Timestamp);
        ledger.Deposit(account, Try(45.75m), Timestamp);
        ledger.Withdraw(account, Try(60.25m), Timestamp);

        foreach (var transaction in ledger.Transactions)
        {
            transaction.Entries.Sum(e =>
                    e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount)
                .ShouldBe(0m);
        }
    }
}
