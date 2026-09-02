using System.Net;
using Microsoft.Extensions.Options;
using Web.Options;

namespace Web.Services;

public sealed class ApiRetryDelegatingHandler : DelegatingHandler
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(20)
    ];

    private readonly IOptionsMonitor<ApiColdStartRecoveryOptions> _optionsMonitor;

    public ApiRetryDelegatingHandler(IOptionsMonitor<ApiColdStartRecoveryOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var maxAttempts = Math.Clamp(options.MaxRetryAttempts, 1, RetryDelays.Length);

        if (!options.Enabled || !CanRetry(request.Method))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        HttpRequestMessage currentRequest = request;

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(currentRequest, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt >= maxAttempts)
                {
                    return response;
                }
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
            }

            response?.Dispose();
            await Task.Delay(RetryDelays[attempt], cancellationToken);
            currentRequest = CloneRequest(request);
        }
    }

    private static bool CanRetry(HttpMethod method)
    {
        return method == HttpMethod.Get
            || method == HttpMethod.Head
            || method == HttpMethod.Options;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
