namespace Web.Services;

public sealed class ClientContextDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                request.Headers.Remove("X-Forwarded-For");
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
            }

            request.Headers.Remove("X-Correlation-ID");
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", context.TraceIdentifier);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
