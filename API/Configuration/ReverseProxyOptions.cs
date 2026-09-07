using System.ComponentModel.DataAnnotations;

namespace API.Configuration;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }

    [Range(1, 5)]
    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];
}
