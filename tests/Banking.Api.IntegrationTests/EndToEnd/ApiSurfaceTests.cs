using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Banking.Api.Contracts;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Accounts.GetStatement;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.EndToEnd;

// Gerçek HTTP pipeline üzerinden müşteri API yüzeyi: yatır/çek, bakiye, ekstre, kapatma, refresh, reversal
[Collection(IntegrationCollection.Name)]
public sealed class ApiSurfaceTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
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
    public async Task CashLifecycle_DepositWithdrawStatementClose_WorksEndToEnd()
    {
        await AuthenticateAsync(_client);
        var account = await CreateAccountAsync(_client);

        // Deposit idempotent: tekrar aynı işlemi döner, ikinci bir yatırım oluşmaz
        var depositKey = $"dep-{Guid.NewGuid():N}";
        var deposit = await CashAsync(_client, account, "deposits", 1_000m, depositKey);
        var replay = await CashAsync(_client, account, "deposits", 1_000m, depositKey);
        replay.TransactionId.ShouldBe(deposit.TransactionId);
        (await GetBalanceAsync(_client, account)).ShouldBe(1_000m);

        await CashAsync(_client, account, "withdrawals", 250m, $"wd-{Guid.NewGuid():N}");
        (await GetBalanceAsync(_client, account)).ShouldBe(750m);

        // Ekstre: en yeni önce, hesabın her ledger kaydı bir satır
        var statement = (await _client.GetFromJsonAsync<AccountStatementResponse>(
            $"/api/accounts/{account}/transactions?page=1&pageSize=10")).ShouldNotBeNull();
        statement.TotalCount.ShouldBe(2);
        statement.Items[0].Description.ShouldBe("Withdrawal");
        statement.Items[0].Direction.ShouldBe("Debit");
        statement.Items[1].Description.ShouldBe("Deposit");

        // Bakiye varken kapatma engellenir, sıfırken izin verilir
        var earlyClose = await _client.PostAsync($"/api/accounts/{account}/close", content: null);
        earlyClose.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await CashAsync(_client, account, "withdrawals", 750m, $"wd-{Guid.NewGuid():N}");
        var close = await _client.PostAsync($"/api/accounts/{account}/close", content: null);
        close.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var closed = (await _client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{account}")).ShouldNotBeNull();
        closed.Status.ShouldBe("Closed");
        closed.Balance.ShouldBe(0m);

        // Hesap listesi (kapalı) hesabı türetilmiş bakiyesiyle gösterir
        var list = (await _client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts")).ShouldNotBeNull();
        list.ShouldHaveSingleItem().Id.ShouldBe(account);
    }

    [Fact]
    public async Task RefreshToken_RotatesAndKillsTheFamilyOnReuse()
    {
        var first = await AuthenticateAsync(_client);

        // Rotation: eski token yeni bir çift satın alır
        var rotated = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken));
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<AuthResponse>()).ShouldNotBeNull();
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        // Tüketilmiş token tekrar kullanılırsa reddedilir ve yeni verilen token dahil her şeyi iptal eder
        (await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(first.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(second.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TransferReversal_ByTheReceiver_RestoresBothBalances()
    {
        await AuthenticateAsync(_client);
        var source = await CreateAccountAsync(_client);
        (await _client.PostAsync($"/api/accounts/{source}/kyc", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await CashAsync(_client, source, "deposits", 500m, $"dep-{Guid.NewGuid():N}");

        using var receiverClient = _factory.CreateClient();
        await AuthenticateAsync(receiverClient);
        var destination = await CreateAccountAsync(receiverClient);

        // Gönderenden 200 transfer et
        using var transferRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, destination, 200m, "TRY")),
        };
        transferRequest.Headers.Add("Idempotency-Key", $"tr-{Guid.NewGuid():N}");
        var transferResponse = await _client.SendAsync(transferRequest);
        transferResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var transfer = (await transferResponse.Content.ReadFromJsonAsync<TransferResponse>()).ShouldNotBeNull();

        // Alıcı ters kayıt atar, iki bakiye de başladığı yere döner
        var reversal = await receiverClient.PostAsync(
            $"/api/transactions/{transfer.TransactionId}/reversal", content: null);
        reversal.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetBalanceAsync(_client, source)).ShouldBe(500m);
        (await GetBalanceAsync(receiverClient, destination)).ShouldBe(0m);

        // Bir işlem en fazla bir kez ters kaydedilebilir
        var secondReversal = await receiverClient.PostAsync(
            $"/api/transactions/{transfer.TransactionId}/reversal", content: null);
        secondReversal.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HealthEndpoints_ReportLiveAndReady()
    {
        (await _client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<AuthResponse> AuthenticateAsync(HttpClient client)
    {
        var email = $"api-{Guid.NewGuid():N}@bank.local";
        const string password = "Api-Pass-123!";
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>()).ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("TRY"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>()).ShouldNotBeNull().Id;
    }

    private static async Task<CashOperationResponse> CashAsync(
        HttpClient client, Guid account, string operation, decimal amount, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts/{account}/{operation}")
        {
            Content = JsonContent.Create(new CashOperationRequest(amount, "TRY")),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CashOperationResponse>()).ShouldNotBeNull();
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client, Guid account) =>
        (await client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{account}")).ShouldNotBeNull().Balance;
}
