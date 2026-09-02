using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Web.Middleware;

public sealed class FrozenAccountMiddleware
{
    private static readonly TimeSpan StatusCheckTimeout = TimeSpan.FromSeconds(3);

    private readonly RequestDelegate _next;

    public FrozenAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IHttpClientFactory httpClientFactory)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsAllowedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await _next(context);
            return;
        }

        var client = httpClientFactory.CreateClient("AccountStatusApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session-status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeoutCts.CancelAfter(StatusCheckTimeout);

            var response = await client.SendAsync(request, timeoutCts.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                if (IsApiLikeRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
                context.Response.Redirect($"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }

            if (response.StatusCode == (HttpStatusCode)StatusCodes.Status423Locked)
            {
                context.Response.Redirect("/Auth/Frozen");
                return;
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException)
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }
        }

        await _next(context);
    }

    private static bool IsAllowedPath(PathString path)
    {
        return path.StartsWithSegments("/Auth/Frozen", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Auth/Logout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiLikeRequest(HttpRequest request)
    {
        return string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase)
            || request.Headers.Accept.Any(x =>
                x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }
}
