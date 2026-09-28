using Domolov.Infrastructure.Scanning;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Domolov.Infrastructure.Workers;

/// <summary>Degraded when Chromium failed to launch within the last 10 minutes.</summary>
public sealed class BrowserHealthCheck(PlaywrightBrowserHost host, TimeProvider clock)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (
            host.LastLaunchError is { } error
            && clock.GetUtcNow() - error.At < TimeSpan.FromMinutes(10)
        )
        {
            return Task.FromResult(
                HealthCheckResult.Degraded($"Chromium failed to launch: {error.Message}")
            );
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
