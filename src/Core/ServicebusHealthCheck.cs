using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Delegateas.DeveloperExperience.Core;

internal sealed class ServicebusHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Worker is alive."));
    }
}
