using System.Diagnostics;

namespace API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers[HeaderName].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(candidate)
            && candidate.Length <= MaxLength
            && candidate.All(IsSafeCharacter))
        {
            return candidate;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsSafeCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '.';
    }
}
