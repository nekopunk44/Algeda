using API.Auth;
using API.Hubs;
using Application.DTOs.DealChat;
using Application.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/deal-chats")]
[Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
public class DealChatsController : ApiControllerBase
{
    private readonly IDealChatService _dealChatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public DealChatsController(
        IDealChatService dealChatService,
        IHubContext<ChatHub> hubContext)
    {
        _dealChatService = dealChatService;
        _hubContext = hubContext;
    }

    [HttpGet("{dealId:guid}/messages")]
    public Task<IActionResult> GetDialog(
        Guid dealId,
        [FromQuery][Range(1, 1000)] int limit = 200)
    {
        return ExecuteAsync(async () =>
        {
            var dialog = await _dealChatService.GetDialogForCurrentUser(
                dealId,
                GetUserEmailOrEmpty(),
                IsAdmin(),
                User.IsInRole(AppRoles.Client),
                User.IsInRole(AppRoles.Realtor),
                limit);

            return Ok(dialog);
        });
    }

    [HttpPost("{dealId:guid}/messages")]
    public Task<IActionResult> Send(Guid dealId, [FromBody] SendDealChatMessageRequest request)
    {
        return ExecuteAsync(async () =>
        {
            var created = await _dealChatService.SendMessageForCurrentUser(
                dealId,
                GetUserEmailOrEmpty(),
                IsAdmin(),
                User.IsInRole(AppRoles.Client),
                User.IsInRole(AppRoles.Realtor),
                request.Content);

            await _hubContext.Clients
                .Group(dealId.ToString("D"))
                .SendAsync("ReceiveDealMessage", created);

            return Ok(created);
        });
    }

    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public Task<IActionResult> GetAdminChats(
        [FromQuery] Guid? dealId = null,
        [FromQuery] Guid? clientId = null,
        [FromQuery] Guid? realtorId = null,
        [FromQuery][Range(1, 200)] int limit = 100)
    {
        return ExecuteAsync(async () => Ok(await _dealChatService.GetAdminChats(
            dealId,
            clientId,
            realtorId,
            limit)));
    }

    [HttpGet("notifications/unread")]
    public Task<IActionResult> GetUnreadNotifications(
        [FromQuery][Range(1, 200)] int limit = 20)
    {
        return ExecuteAsync(async () =>
        {
            var notifications = await _dealChatService.GetUnreadNotificationsForCurrentUser(
                GetUserEmailOrEmpty(),
                IsAdmin(),
                User.IsInRole(AppRoles.Client),
                User.IsInRole(AppRoles.Realtor),
                limit);

            return Ok(notifications);
        });
    }

    [HttpPost("notifications/mark-read")]
    public Task<IActionResult> MarkNotificationsRead()
    {
        return ExecuteAsync(async () =>
        {
            var updated = await _dealChatService.MarkAllNotificationsAsReadForCurrentUser(
                GetUserEmailOrEmpty(),
                IsAdmin(),
                User.IsInRole(AppRoles.Client),
                User.IsInRole(AppRoles.Realtor));

            return Ok(new { updated });
        });
    }

    private string GetUserEmailOrEmpty()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;
    }

    private bool IsAdmin()
    {
        return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
    }
}
