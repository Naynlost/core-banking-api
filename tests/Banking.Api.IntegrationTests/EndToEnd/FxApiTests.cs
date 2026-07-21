using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Banking.Api.Contracts;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Fx;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.EndToEnd;

// FX uçları gerçek HTTP pipeline üzerinden: kur sorgusu herkese açık (girişli),
// stok yükleme yalnızca hazine rolüne açık, çapraz kur transferi uçtan uca çalışıyor.
[Collection(IntegrationCollection.Name)]
public sealed class FxApiTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private const string TreasuryEmail = "treasury@bank.local";
    private const string Password = "Fx-Pass-123!";

    private BankingApiFactory _factory = null!;
    private HttpClient _customer = null!;
    private HttpClient _treasury = null!;

    public async Task InitializeAsync()
    {
        _factory = new BankingApiFactory(infrastructure, extraSettings: new Dictionary<string, string>
        {
            ["Treasury:OperatorEmails:0"] = TreasuryEmail,

            // Okunabilir sayılar: 1 USD = 40 TRY
            ["Fx:BaseCurrency"] = "TRY",
            ["Fx:Rates:USD"] = "40",
        });
        _customer = _factory.CreateClient();
        _treasury = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _customer.Dispose();
        _treasury.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task FxFlow_QuoteFundAndCrossCurrencyTransfer_WorkEndToEnd()
    {
        await RegisterAndLoginAsync(_customer, $"fx-customer-{Guid.NewGuid():N}@bank.local");

        // Kur sorgusu: 2.000 TRY = 50 USD
        var quote = (await _customer.GetFromJsonAsync<FxQuoteResponse>(
            "/api/fx/quote?from=TRY&to=USD&amount=2000")).ShouldNotBeNull();
        quote.ConvertedAmount.ShouldBe(50m);

        // Müşteri bankanın döviz stoğunu besleyemez
        (await FundAsync(_customer, 1_000m, "USD")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Hazine kullanıcısı besleyebilir
        await RegisterAndLoginAsync(_treasury, TreasuryEmail);
        (await FundAsync(_treasury, 1_000m, "USD")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // TRY hesabından USD hesabına transfer
        var source = await CreateAccountAsync(_customer, "TRY");
        (await _customer.PostAsync($"/api/accounts/{source}/kyc", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await DepositAsync(_customer, source, 5_000m, "TRY");

        var destination = await CreateAccountAsync(_customer, "USD");

        using var transfer = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new TransferRequest(source, destination, 2_000m, "TRY")),
        };
        transfer.Headers.Add("Idempotency-Key", $"fx-{Guid.NewGuid():N}");
        (await _customer.SendAsync(transfer)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Gönderen TRY kaybetti, alıcı kur karşılığı USD aldı
        (await BalanceAsync(_customer, source)).ShouldBe(3_000m);
        (await BalanceAsync(_customer, destination)).ShouldBe(50m);
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

    private static async Task<HttpResponseMessage> FundAsync(HttpClient client, decimal amount, string currency)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/fx/positions")
        {
            Content = JsonContent.Create(new FundFxPositionRequest(amount, currency)),
        };
        request.Headers.Add("Idempotency-Key", $"fund-{Guid.NewGuid():N}");
        return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string currency)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(currency));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreateAccountResponse>()).ShouldNotBeNull().Id;
    }

    private static async Task DepositAsync(HttpClient client, Guid account, decimal amount, string currency)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts/{account}/deposits")
        {
            Content = JsonContent.Create(new CashOperationRequest(amount, currency)),
        };
        request.Headers.Add("Idempotency-Key", $"dep-{Guid.NewGuid():N}");
        (await client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid account) =>
        (await client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{account}")).ShouldNotBeNull().Balance;
}
