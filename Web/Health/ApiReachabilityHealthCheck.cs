using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web.Health;

public sealed class ApiReachabilityHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiWakeup");
            using var response = await client.GetAsync("health/live", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("API доступен.")
                : HealthCheckResult.Unhealthy($"API вернул статус {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("API недоступен.", exception);
        }
    }
}
