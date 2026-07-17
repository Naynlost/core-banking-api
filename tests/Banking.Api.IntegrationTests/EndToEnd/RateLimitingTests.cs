using System.Net;
using System.Net.Http.Json;
using Banking.Api.Contracts;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.EndToEnd;

[Collection(IntegrationCollection.Name)]
public sealed class RateLimitingTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private const int PermitLimit = 3;

    private BankingApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new BankingApiFactory(infrastructure, authRateLimit: PermitLimit);
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
    public async Task Login_BeyondThePerIpBudget_IsRejectedWith429()
    {
        var attempt = new LoginRequest("nobody@bank.local", "Wrong-Pass-1!");

        // The budget only bounds the attempt rate; failed credentials stay 401.
        for (var i = 0; i < PermitLimit; i++)
        {
            (await _client.PostAsJsonAsync("/api/auth/login", attempt))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await _client.PostAsJsonAsync("/api/auth/login", attempt))
            .StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
