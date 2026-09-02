using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Middleware;

public sealed class FrozenAccountMiddleware
{
    private readonly RequestDelegate _next;

    public FrozenAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdentityAccountManager identityAccountManager)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !TryGetCurrentUserId(context.User, out var userId))
        {
            await _next(context);
            return;
        }

        bool isFrozen;
        try
        {
            isFrozen = await identityAccountManager.IsFrozen(userId, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        if (isFrozen)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = "Аккаунт заморожен",
                Detail = "Ваш аккаунт заморожен. Обратитесь к администратору."
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal user, out Guid userId)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(raw, out userId);
    }
}
