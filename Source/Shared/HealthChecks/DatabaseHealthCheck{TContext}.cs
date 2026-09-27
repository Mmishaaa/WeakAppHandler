using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.HealthChecks;

public sealed class DatabaseHealthCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        if (scope.ServiceProvider.GetService<IDbContextFactory<TContext>>() is { } factory)
        {
            await using var created = await factory.CreateDbContextAsync(cancellationToken);

            return await ProbeAsync(created, cancellationToken);
        }

        return await ProbeAsync(scope.ServiceProvider.GetRequiredService<TContext>(), cancellationToken);
    }

    private static async Task<HealthCheckResult> ProbeAsync(DbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database is unreachable.");
}
