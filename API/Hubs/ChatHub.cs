using API.Auth;
using Application.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Hubs
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    public class ChatHub : Hub
    {
        private readonly IDealChatService _dealChatService;

        public ChatHub(IDealChatService dealChatService)
        {
            _dealChatService = dealChatService;
        }

        public async Task JoinDealChat(Guid dealId)
        {
            var canAccess = await _dealChatService.CanAccessDealChat(
                dealId,
                GetUserEmailOrEmpty(),
                IsInRole(AppRoles.Admin),
                IsInRole(AppRoles.Client),
                IsInRole(AppRoles.Realtor));

            if (!canAccess)
                throw new HubException("Access denied for this deal chat.");

            await Groups.AddToGroupAsync(Context.ConnectionId, dealId.ToString("D"));
        }

        public async Task LeaveDealChat(Guid dealId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, dealId.ToString("D"));
        }

        private bool IsInRole(string role)
        {
            return Context.User?.IsInRole(role) == true;
        }

        private string GetUserEmailOrEmpty()
        {
            var user = Context.User;
            if (user is null)
                return string.Empty;

            return user.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }
    }
}
