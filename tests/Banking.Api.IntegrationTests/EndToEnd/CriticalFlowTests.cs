using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Banking.Api.Contracts;
using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.EndToEnd;

// Gerçek HTTP pipeline üzerinden kritik akış: kayıt→giriş→hesap→KYC→transfer→outbox→iki consumer
[Collection(IntegrationCollection.Name)]
public sealed class CriticalFlowTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private BankingApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new BankingApiFactory(infrastructure);
        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RegisterLoginOpenAccountsTransfer_EventIsPublishedAndConsumed()
    {
        // Kayıt
        var email = $"e2e-{Guid.NewGuid():N}@bank.local";
        const string password = "E2e-Pass-123!";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Giriş
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>()).ShouldNotBeNull();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Hesaplar + kaynak hesabın KYC'si
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();
        var kyc = await _client.PostAsync($"/api/accounts/{source}/kyc", content: null);
        kyc.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Bakiye: API'de kasadan para basan bir endpoint yok (bilinçli);
        // arrange adımı gerçek bir depoziti altyapı üzerinden kaydeder.
        await FundAsync(source, 1_000m);

        // Transfer
        using var transferRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, destination, 250m, "TRY")),
        };
        transferRequest.Headers.Add("Idempotency-Key", $"e2e-{Guid.NewGuid():N}");
        var transferResponse = await _client.SendAsync(transferRequest);
        transferResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var transfer = (await transferResponse.Content.ReadFromJsonAsync<TransferResponse>()).ShouldNotBeNull();

        // Olay: outbox'tan yayınlandı ve iki consumer da (bildirim + fraud) işledi.
        var messageId = await WaitForPublishedOutboxMessageAsync(transfer.TransactionId);
        await TestBank.WaitUntilAsync(
            async () =>
            {
                await using var scope = _factory.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
                return await context.Set<InboxMessage>().CountAsync(m => m.MessageId == messageId) == 2;
            },
            $"both consumers to process outbox message {messageId}");
    }

    private async Task<Guid> CreateAccountAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("TRY"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>()).ShouldNotBeNull().Id;
    }

    // API ile açılan hesap için kasaya karşı dengeli gerçek bir deposit kaydeder
    private async Task FundAsync(Guid accountId, decimal amount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = (await accounts.GetByIdAsync(new AccountId(accountId), CancellationToken.None))
            .ShouldNotBeNull();
        var cash = Account.OpenCash(Currency.Try);
        await accounts.AddAsync(cash, CancellationToken.None);

        var money = Money.Create(amount, Currency.Try).Value;
        var deposit = Transaction.Create(
            "Deposit",
            DateTimeOffset.UtcNow,
            [
                new EntrySpec(cash.Id, money, EntryDirection.Debit),
                new EntrySpec(account.Id, money, EntryDirection.Credit),
            ]).Value;

        cash.RecordMovement();
        account.RecordMovement();
        await transactions.AddAsync(deposit, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<Guid> WaitForPublishedOutboxMessageAsync(Guid transactionId)
    {
        Guid messageId = default;
        await TestBank.WaitUntilAsync(
            async () =>
            {
                await using var scope = _factory.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
                // Payload jsonb, SQL yerine istemci tarafında eşleştir
                var candidates = await context.Set<OutboxMessage>()
                    .Where(m => m.ProcessedAt != null)
                    .ToListAsync();

                var match = candidates.SingleOrDefault(m => m.Payload.Contains(transactionId.ToString()));
                if (match is null)
                {
                    return false;
                }

                messageId = match.Id;
                return true;
            },
            $"the outbox message for transaction {transactionId} to be published");

        return messageId;
    }
}
