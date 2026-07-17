using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.Ledgers;

public class ReversalPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly Ledger _ledger = new();
    private readonly Account _source;
    private readonly Account _destination;
    private readonly Transaction _transfer;

    public ReversalPolicyTests()
    {
        _source = Account.Open("user-1", Currency.Try).Value;
        _source.CompleteKyc();
        _destination = Account.Open("user-2", Currency.Try).Value;

        _ledger.Deposit(_source, Money.Create(100m, Currency.Try).Value, T0);
        _transfer = _ledger.Transfer(
            _source, _destination, Money.Create(40m, Currency.Try).Value, T0.AddMinutes(1)).Value;
    }

    private Account[] Involved => [_source, _destination];

    [Fact]
    public void Reverse_ByCreditedAccount_FlipsEveryEntryAndRestoresBalances()
    {
        var reversal = _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(2));

        reversal.IsSuccess.ShouldBeTrue();
        reversal.Value.ReversesTransactionId.ShouldBe(_transfer.Id);
        reversal.Value.Description.ShouldBe(ReversalPolicy.ReversalDescription);
        reversal.Value.Entries.ShouldContain(e =>
            e.AccountId == _destination.Id && e.Direction == EntryDirection.Debit);
        reversal.Value.Entries.ShouldContain(e =>
            e.AccountId == _source.Id && e.Direction == EntryDirection.Credit);

        _ledger.GetBalance(_source).Amount.ShouldBe(100m);
        _ledger.GetBalance(_destination).Amount.ShouldBe(0m);
    }

    [Fact]
    public void Reverse_Twice_FailsWithAlreadyReversed()
    {
        _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(2)).IsSuccess.ShouldBeTrue();

        var second = _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(3));

        second.IsFailure.ShouldBeTrue();
        second.Error.ShouldBe(ReversalErrors.AlreadyReversed);
    }

    [Fact]
    public void Reverse_AReversal_FailsWithNotReversible()
    {
        var reversal = _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(2)).Value;

        var reversalOfReversal = _ledger.Reverse(reversal, _source, Involved, T0.AddMinutes(3));

        reversalOfReversal.IsFailure.ShouldBeTrue();
        reversalOfReversal.Error.ShouldBe(ReversalErrors.NotReversible);
    }

    [Fact]
    public void Reverse_ByTheDebitedAccount_FailsBecauseOnlyTheReceiverGivesMoneyBack()
    {
        var reversal = _ledger.Reverse(_transfer, _source, Involved, T0.AddMinutes(2));

        reversal.IsFailure.ShouldBeTrue();
        reversal.Error.ShouldBe(ReversalErrors.OnlyCreditedAccountCanReverse);
    }

    [Fact]
    public void Reverse_WhenTheReceiverAlreadySpentTheMoney_FailsWithInsufficientFunds()
    {
        _ledger.Withdraw(_destination, Money.Create(40m, Currency.Try).Value, T0.AddMinutes(2))
            .IsSuccess.ShouldBeTrue();

        var reversal = _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(3));

        reversal.IsFailure.ShouldBeTrue();
        reversal.Error.ShouldBe(LedgerErrors.InsufficientFunds);
    }

    [Fact]
    public void Reverse_WhenAnInvolvedAccountIsClosed_Fails()
    {
        _source.Close();

        var reversal = _ledger.Reverse(_transfer, _destination, Involved, T0.AddMinutes(2));

        reversal.IsFailure.ShouldBeTrue();
        reversal.Error.ShouldBe(AccountErrors.Closed);
    }
}
