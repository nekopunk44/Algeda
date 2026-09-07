using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Health;

public sealed class DatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Database is unavailable.");
            }

            var pendingMigrations = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .Take(1)
                .Any();

            return pendingMigrations
                ? HealthCheckResult.Unhealthy("Database schema is not current.")
                : HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database readiness check failed.");
            return HealthCheckResult.Unhealthy("Database readiness check failed.");
        }
    }
}
