using Microsoft.Extensions.Options;
using Web.Options;

namespace Web.Services;

public sealed class ApiWakeupHostedService : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(7),
        TimeSpan.FromSeconds(12),
        TimeSpan.FromSeconds(18),
        TimeSpan.FromSeconds(25)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiWakeupHostedService> _logger;
    private readonly IOptionsMonitor<ApiColdStartRecoveryOptions> _optionsMonitor;

    public ApiWakeupHostedService(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiWakeupHostedService> logger,
        IOptionsMonitor<ApiColdStartRecoveryOptions> optionsMonitor)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("API cold start recovery is disabled.");
            return;
        }

        await Task.Yield();

        var client = _httpClientFactory.CreateClient("ApiWakeup");
        await WakeApiAsync(client, stoppingToken);

        var keepAwakeInterval = TimeSpan.FromMinutes(options.KeepApiAwakeIntervalMinutes);
        if (keepAwakeInterval <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(keepAwakeInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await WakeApiAsync(client, stoppingToken);
        }
    }

    private async Task WakeApiAsync(HttpClient client, CancellationToken stoppingToken)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("api/health/db", stoppingToken);
                if ((int)response.StatusCode < 500)
                {
                    _logger.LogInformation("API wakeup request completed with status {StatusCode}.", (int)response.StatusCode);
                    return;
                }

                _logger.LogInformation("API wakeup request returned {StatusCode}. Retrying if possible.", (int)response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogInformation(ex, "API wakeup request failed. Retrying if possible.");
            }
            catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("API wakeup request timed out. Retrying if possible.");
            }

            if (attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], stoppingToken);
            }
        }
    }
}
