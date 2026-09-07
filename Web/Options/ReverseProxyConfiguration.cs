using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Web.Options;

public static class ReverseProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions target, ReverseProxyOptions source)
    {
        target.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        target.ForwardLimit = source.ForwardLimit;

        foreach (var value in source.KnownProxies)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException($"Некорректный ReverseProxy:KnownProxies: '{value}'.");
            }

            target.KnownProxies.Add(address);
        }

        foreach (var value in source.KnownNetworks)
        {
            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !IPAddress.TryParse(parts[0], out var prefix)
                || !int.TryParse(parts[1], out var prefixLength))
            {
                throw new InvalidOperationException(
                    $"Некорректный ReverseProxy:KnownNetworks: '{value}'. Ожидается CIDR.");
            }

            target.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
        }

        if (source.Enabled && source.KnownProxies.Length == 0 && source.KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException(
                "ReverseProxy включён, но не задан ни один KnownProxies или KnownNetworks.");
        }
    }
}
