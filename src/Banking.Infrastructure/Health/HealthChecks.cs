using Banking.Infrastructure.Messaging;
using Banking.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Banking.Infrastructure.Health;

internal sealed class PostgresHealthCheck(BankingDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext, CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL did not accept a connection.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
        }
    }
}

internal sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connections.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unreachable.", exception);
        }
    }
}
