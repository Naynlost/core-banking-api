using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.Accounts;

public class AccountTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_WithoutOwner_Fails(string owner)
    {
        var result = Account.Open(owner, Currency.Try);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.OwnerRequired);
    }

    [Fact]
    public void Open_WithValidOwner_CreatesActiveLiabilityAccount()
    {
        var result = Account.Open("  Ayşe Yılmaz  ", Currency.Try);

        result.IsSuccess.ShouldBeTrue();
        var account = result.Value;
        account.Owner.ShouldBe("Ayşe Yılmaz");
        account.Currency.ShouldBe(Currency.Try);
        account.Status.ShouldBe(AccountStatus.Active);
        account.Type.ShouldBe(AccountType.Liability);
        account.KycStatus.ShouldBe(KycStatus.Pending);
        account.DailyTransferLimit.ShouldBe(Account.DefaultDailyTransferLimit);
    }

    [Fact]
    public void OpenCash_CreatesActiveAssetAccount()
    {
        var cash = Account.OpenCash(Currency.Try);

        cash.Type.ShouldBe(AccountType.Asset);
        cash.Status.ShouldBe(AccountStatus.Active);
        cash.IsKycVerified.ShouldBeTrue(); // internal account, no customer to verify
    }

    [Fact]
    public void CompleteKyc_OnPendingAccount_VerifiesAndBumpsVersion()
    {
        var account = Account.Open("Ayşe", Currency.Try).Value;

        var result = account.CompleteKyc();

        result.IsSuccess.ShouldBeTrue();
        account.KycStatus.ShouldBe(KycStatus.Verified);
        account.Version.ShouldBe(1);
    }

    [Fact]
    public void CompleteKyc_Twice_Fails()
    {
        var account = Account.Open("Ayşe", Currency.Try).Value;
        account.CompleteKyc();

        var result = account.CompleteKyc();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycAlreadyVerified);
    }

    [Fact]
    public void CompleteKyc_OnClosedAccount_Fails()
    {
        var account = Account.Open("Ayşe", Currency.Try).Value;
        account.Close();

        var result = account.CompleteKyc();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
        account.KycStatus.ShouldBe(KycStatus.Pending);
    }

    [Fact]
    public void Close_ActiveAccount_Succeeds()
    {
        var account = Account.Open("Ayşe", Currency.Try).Value;

        var result = account.Close();

        result.IsSuccess.ShouldBeTrue();
        account.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void Close_AlreadyClosedAccount_Fails()
    {
        var account = Account.Open("Ayşe", Currency.Try).Value;
        account.Close();

        var result = account.Close();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.AlreadyClosed);
    }
}
