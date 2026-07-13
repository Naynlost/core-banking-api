using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Risk;

/// <summary>Stage 6 risk controls against real PostgreSQL: daily limit and KYC gating.</summary>
[Collection(IntegrationCollection.Name)]
public sealed class RiskControlsTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync() =>
        _provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Transfer_BeyondTheDailyLimit_IsRejected()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: 30_000m);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b");

        var first = await TestBank.TransferAsync(_provider, source, destination, 15_000m);
        var second = await TestBank.TransferAsync(_provider, source, destination, 6_000m); // 21.000 > 20.000

        first.IsSuccess.ShouldBeTrue();
        second.IsFailure.ShouldBeTrue();
        second.Error.ShouldBe(LedgerErrors.DailyLimitExceeded);

        // Funds were there (balance 15.000 ≥ 6.000): only the limit blocked it.
        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(15_000m);
        (await TestBank.GetBalanceAsync(_provider, destination)).ShouldBe(15_000m);
    }

    [Fact]
    public async Task Transfer_UpToExactlyTheDailyLimit_Succeeds()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: 30_000m);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b");

        var first = await TestBank.TransferAsync(_provider, source, destination, 15_000m);
        var second = await TestBank.TransferAsync(_provider, source, destination, 5_000m); // exactly 20.000

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(10_000m);
    }

    [Fact]
    public async Task Transfer_FromKycPendingAccount_IsRejected()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: 100m, kycVerified: false);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b");

        var result = await TestBank.TransferAsync(_provider, source, destination, 10m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycNotVerified);
        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(100m);
    }

    [Fact]
    public async Task Transfer_ToKycPendingAccount_Succeeds()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: 100m);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b", kycVerified: false);

        var result = await TestBank.TransferAsync(_provider, source, destination, 40m);

        result.IsSuccess.ShouldBeTrue();
        (await TestBank.GetBalanceAsync(_provider, destination)).ShouldBe(40m);
    }
}
