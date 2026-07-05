using AutoArchive.Core.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AutoArchive.Worker.HealthChecks;

public sealed class ArchiveRootHealthCheck(IOptions<ArchiveOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var root = options.Value.RootPath;
        if (!Directory.Exists(root))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Archive root '{root}' does not exist."));
        }

        try
        {
            var probePath = Path.Combine(root, $".autoarchive-health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (IOException ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Archive root '{root}' is not writable.", ex));
        }
    }
}
