using System.Threading.RateLimiting;
using Banking.Api.Middleware;
using Banking.Api.OpenApi;
using Banking.Application;
using Banking.Infrastructure;
using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Yapılandırılmış loglama: her satır CorrelationId/TraceId gibi alanları property olarak taşır
builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Kimlik doğrulama endpoint'lerinde IP başına dakikalık limit; testler için config'ten gevşetilebilir
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
            // Metrik scrape endpoint'i gerçek trafiği gürültüye boğardı
            options.Filter = context => context.Request.Path != "/metrics")
        .AddHttpClientInstrumentation()
        .AddNpgsql() // her EF Core komutu kendi handler'ının altında span olarak görünür
        .AddSource(BankingDiagnostics.ActivitySourceName)
        .AddSource(MessagingDiagnostics.ActivitySourceName)
        .AddOtlpExporter()) // Jaeger (docker-compose) varsayılan olarak localhost:4317'yi dinler
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(BankingDiagnostics.MeterName)
        .AddPrometheusExporter());

var app = builder.Build();

// Varsayılan olarak KAPALIDIR: şema değişikliği üretimde ayrı bir dağıtım adımıdır.
// Yalnızca "docker compose --profile full up" demosu, şemayı kendi kaldırabilsin diye açar.
if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.GetRequiredService<BankingDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(); // her istek için tek yapılandırılmış tamamlanma logu

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar/v1'de interaktif API referansı
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapPrometheusScrapingEndpoint(); // Prometheus scraper için /metrics

// Liveness = süreç cevap veriyor mu; readiness = Postgres ve RabbitMQ erişilebilir mi
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

// WebApplicationFactory'nin (uçtan uca testler) host'u ayağa kaldırabilmesi için.
public partial class Program;
