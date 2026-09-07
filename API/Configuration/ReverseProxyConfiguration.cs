using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace API.Configuration;

public static class ReverseProxyConfiguration
{
    public static void Configure(
        ForwardedHeadersOptions forwardedHeaders,
        ReverseProxyOptions options)
    {
        forwardedHeaders.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        forwardedHeaders.ForwardLimit = options.ForwardLimit;

        foreach (var value in options.KnownProxies)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException($"Некорректный ReverseProxy:KnownProxies: '{value}'.");
            }

            forwardedHeaders.KnownProxies.Add(address);
        }

        foreach (var value in options.KnownNetworks)
        {
            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !IPAddress.TryParse(parts[0], out var prefix)
                || !int.TryParse(parts[1], out var prefixLength))
            {
                throw new InvalidOperationException(
                    $"Некорректный ReverseProxy:KnownNetworks: '{value}'. Ожидается CIDR, например 172.30.0.0/24.");
            }

            forwardedHeaders.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
        }

        if (options.Enabled
            && options.KnownProxies.Length == 0
            && options.KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException(
                "ReverseProxy включён, но не задан ни один KnownProxies или KnownNetworks.");
        }
    }
}
