using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Banking.Api.Contracts;
using Banking.Api.IntegrationTests.EndToEnd;
using Banking.Application.Fraud.ListFraudAlerts;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Risk;

// Gerçek HTTP pipeline üzerinden back-office inceleme döngüsü: müşteri token'ı reddedilir, reviewer görüp kapatır
[Collection(IntegrationCollection.Name)]
public sealed class FraudReviewTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private const string ReviewerEmail = "reviewer@bank.local";
    private const string Password = "Review-Pass-123!";

    private BankingApiFactory _factory = null!;
    private HttpClient _customer = null!;
    private HttpClient _reviewer = null!;

    public async Task InitializeAsync()
    {
        _factory = new BankingApiFactory(infrastructure, extraSettings: new Dictionary<string, string>
        {
            ["FraudReview:ReviewerEmails:0"] = ReviewerEmail,
        });
        _customer = _factory.CreateClient();
        _reviewer = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _customer.Dispose();
        _reviewer.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task FraudReviewLoop_FlagListResolve_WorksEndToEndAndIsRoleGated()
    {
        // Normal bir müşteri kuyruğu listeleyemez bile
        await RegisterAndLoginAsync(_customer, $"customer-{Guid.NewGuid():N}@bank.local");
        (await _customer.GetAsync("/api/fraud-alerts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Müşteri eşik üstü bir transfer gönderir, işaretlenir
        var source = await CreateFundedAccountAsync(_customer, 16_000m);
        var destination = await CreateAccountAsync(_customer);
        using var transferRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, destination, 15_000m, "TRY")),
        };
        transferRequest.Headers.Add("Idempotency-Key", $"fr-{Guid.NewGuid():N}");
        var transferResponse = await _customer.SendAsync(transferRequest);
        transferResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var transactionId = (await transferResponse.Content.ReadFromJsonAsync<TransferResponse>())
            .ShouldNotBeNull().TransactionId;

        // Tarama asenkron olduğundan reviewer uyarıyı açık kuyrukta biraz sonra görür
        await RegisterAndLoginAsync(_reviewer, ReviewerEmail);
        FraudAlertResponse alert = null!;
        await TestBank.WaitUntilAsync(
            async () =>
            {
                var page = await _reviewer.GetFromJsonAsync<FraudAlertListResponse>(
                    "/api/fraud-alerts?status=Open&pageSize=50");
                alert = page!.Items.FirstOrDefault(a => a.TransactionId == transactionId)!;
                return alert is not null;
            },
            $"an open fraud alert for transaction {transactionId}");
        alert.Rule.ShouldBe("amount_above_threshold");

        // Karar bir kez verilir, sonradan değiştirilemez
        var resolve = await _reviewer.PostAsJsonAsync(
            $"/api/fraud-alerts/{alert.Id}/resolve",
            new ResolveFraudAlertRequest("Dismissed", "test transfer, cleared with the customer"));
        resolve.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await _reviewer.PostAsJsonAsync(
            $"/api/fraud-alerts/{alert.Id}/resolve", new ResolveFraudAlertRequest("Confirmed", null));
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resolved = await _reviewer.GetFromJsonAsync<FraudAlertListResponse>(
            "/api/fraud-alerts?status=Dismissed&pageSize=50");
        resolved!.Items.ShouldContain(a => a.Id == alert.Id && a.ResolutionNote != null);
    }

    private static async Task RegisterAndLoginAsync(HttpClient client, string email)
    {
        (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>()).ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("TRY"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>()).ShouldNotBeNull().Id;
    }

    private static async Task<Guid> CreateFundedAccountAsync(HttpClient client, decimal amount)
    {
        var account = await CreateAccountAsync(client);
        (await client.PostAsync($"/api/accounts/{account}/kyc", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var deposit = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts/{account}/deposits")
        {
            Content = JsonContent.Create(new CashOperationRequest(amount, "TRY")),
        };
        deposit.Headers.Add("Idempotency-Key", $"dep-{Guid.NewGuid():N}");
        (await client.SendAsync(deposit)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return account;
    }
}
