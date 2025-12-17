using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ESB.DeveloperExperience;

internal sealed class ServiceBusHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Worker is alive."));
    }
}
