using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Accounts.CompleteKyc;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Application.Tests.Accounts;

public class CompleteKycCommandHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Account _account;

    public CompleteKycCommandHandlerTests()
    {
        _account = Account.Open("user-1", Currency.Try).Value;
        _accounts.AddAsync(_account, CancellationToken.None);
    }

    private CompleteKycCommandHandler BuildHandler() => new(_accounts, _unitOfWork);

    [Fact]
    public async Task Handle_OnPendingAccount_VerifiesAndSaves()
    {
        var result = await BuildHandler().HandleAsync(
            new CompleteKycCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _account.KycStatus.ShouldBe(KycStatus.Verified);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhenAccountBelongsToAnotherUser_ReturnsNotFound()
    {
        var result = await BuildHandler().HandleAsync(
            new CompleteKycCommand(_account.Id.Value, "user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
        _account.KycStatus.ShouldBe(KycStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var result = await BuildHandler().HandleAsync(
            new CompleteKycCommand(Guid.NewGuid(), "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAlreadyVerified_FailsWithoutSaving()
    {
        _account.CompleteKyc();

        var result = await BuildHandler().HandleAsync(
            new CompleteKycCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycAlreadyVerified);
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_OnConcurrencyConflict_ReturnsConflict()
    {
        _unitOfWork.PendingFailures.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));

        var result = await BuildHandler().HandleAsync(
            new CompleteKycCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.Conflict);
    }
}
