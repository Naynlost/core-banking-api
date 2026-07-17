using System.Threading.RateLimiting;
using Banking.Api.Middleware;
using Banking.Api.OpenApi;
using Banking.Application;
using Banking.Infrastructure;
using Banking.Infrastructure.Messaging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging: every line carries its properties (CorrelationId,
// TraceId, ...) instead of burying them in the message text.
builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Brute-force guard on the credential endpoints: a small per-IP budget per
// minute. The limit is configuration so tests can loosen it.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:AuthPermitLimit", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "banking-api",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            // The metrics scrape endpoint would drown real traffic in noise.
            options.Filter = context => context.Request.Path != "/metrics")
        .AddHttpClientInstrumentation()
        .AddNpgsql() // database spans: every EF Core command shows up under its handler
        .AddSource(BankingDiagnostics.ActivitySourceName)
        .AddSource(MessagingDiagnostics.ActivitySourceName)
        .AddOtlpExporter()) // Jaeger (docker-compose) listens on localhost:4317 by default
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(BankingDiagnostics.MeterName)
        .AddPrometheusExporter());

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(); // one structured completion event per request

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive API reference at /scalar/v1
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapPrometheusScrapingEndpoint(); // /metrics for the Prometheus scraper

// Liveness = the process answers; readiness = PostgreSQL and RabbitMQ reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

// WebApplicationFactory'nin (uçtan uca testler) host'u ayağa kaldırabilmesi için.
public partial class Program;
