using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Persistence;

[Collection(IntegrationCollection.Name)]
public sealed class AccountPersistenceTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync() =>
        _provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Account_CanBeSavedAndReadBack()
    {
        var account = Account.Open("Ayşe Yılmaz", Currency.Try).Value;

        await using (var scope = _provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            await repository.AddAsync(account, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
                .SaveChangesAsync(CancellationToken.None);
        }

        // Taze scope => taze DbContext, hesap gerçekten veritabanından gelir
        await using (var scope = _provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var loaded = (await repository.GetByIdAsync(account.Id, CancellationToken.None)).ShouldNotBeNull();

            loaded.Id.ShouldBe(account.Id);
            loaded.Owner.ShouldBe("Ayşe Yılmaz");
            loaded.Currency.ShouldBe(Currency.Try);
            loaded.Type.ShouldBe(AccountType.Liability);
            loaded.Status.ShouldBe(AccountStatus.Active);
        }
    }
}
