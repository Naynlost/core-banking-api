using System.Text;
using Banking.Application.Abstractions;
using Banking.Infrastructure.Identity;
using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Messaging.Consumers;
using Banking.Infrastructure.Persistence;
using Banking.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Banking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddPersistence(configuration)
            .AddIdentityAndJwtAuth(configuration)
            .AddMessaging(configuration);
    }

    private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddScoped<IOutbox, Outbox>();

        services.AddHostedService<OutboxPublisher>();
        services.AddHostedService<TransferNotificationConsumer>();
        services.AddHostedService<FraudScreeningConsumer>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BankingDb")
            ?? throw new InvalidOperationException(
                "Connection string 'BankingDb' is not configured. " +
                "Set it in appsettings or via the ConnectionStrings__BankingDb environment variable.");

        services.AddDbContext<BankingDbContext>(options =>
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static IServiceCollection AddIdentityAndJwtAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Configuration section 'Jwt' is missing.");

        if (string.IsNullOrWhiteSpace(jwt.Secret))
        {
            throw new InvalidOperationException(
                "JWT secret is not configured. Set the Jwt__Secret environment variable "
                + "(or 'dotnet user-secrets set Jwt:Secret ...' in development).");
        }

        if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes for HMAC-SHA256.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<BankingDbContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                });

        services.AddAuthorization();

        return services;
    }
}
