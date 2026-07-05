using AutoArchive.Core.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AutoArchive.Worker.HealthChecks;

public sealed class OllamaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
            using var response = await client.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Ollama returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Could not reach Ollama.", ex);
        }
    }
}
