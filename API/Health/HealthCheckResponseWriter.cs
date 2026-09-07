using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Health;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";

        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestampUtc = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
