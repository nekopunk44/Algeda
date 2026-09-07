using API.Auth;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.DTOs.Auth;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ApiController]
    [Route("api/access-management/users")]
    public class AccessManagementController : ApiControllerBase
    {
        private readonly UserAccessManagementService _service;
        private readonly UserSessionService _userSessionService;

        public AccessManagementController(
            UserAccessManagementService service,
            UserSessionService userSessionService)
        {
            _service = service;
            _userSessionService = userSessionService;
        }

        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery][Range(1, 500)] int limit = 200,
            [FromQuery] bool excludeClients = true)
        {
            return ExecuteAsync(async () =>
            {
                var items = await _service.GetUsers(search, role, limit, excludeClients);
                return Ok(items);
            });
        }

        [HttpPost("{userId:guid}/roles/{role}")]
        public Task<IActionResult> AssignRole(Guid userId, string role)
        {
            return ExecuteAsync(async () =>
            {
                var result = await _service.AssignRole(GetCurrentUserId(), userId, role);
                await _userSessionService.RevokeAll(userId, cancellationToken: HttpContext.RequestAborted);
                return Ok(result);
            });
        }

        [HttpDelete("{userId:guid}/roles/{role}")]
        public Task<IActionResult> RemoveRole(Guid userId, string role)
        {
            return ExecuteAsync(async () =>
            {
                var result = await _service.RemoveRole(GetCurrentUserId(), userId, role);
                await _userSessionService.RevokeAll(userId, cancellationToken: HttpContext.RequestAborted);
                return Ok(result);
            });
        }

        [HttpPost("{userId:guid}/transfer-super-admin")]
        public Task<IActionResult> TransferSuperAdmin(Guid userId, [FromBody] TransferSuperAdminRequest request)
        {
            var normalizedRequest = request with { TargetUserId = userId };
            return ExecuteAsync(async () =>
            {
                var actorUserId = GetCurrentUserId();
                var result = await _service.TransferSuperAdmin(actorUserId, normalizedRequest);
                await _userSessionService.RevokeAll(userId, cancellationToken: HttpContext.RequestAborted);
                if (actorUserId != userId)
                {
                    await _userSessionService.RevokeAll(actorUserId, cancellationToken: HttpContext.RequestAborted);
                }

                return Ok(result);
            });
        }

        [HttpPost("{userId:guid}/freeze")]
        public Task<IActionResult> FreezeAccount(Guid userId)
        {
            return ExecuteAsync(async () =>
            {
                var result = await _service.SetAccountFrozen(GetCurrentUserId(), userId, true);
                await _userSessionService.RevokeAll(userId, cancellationToken: HttpContext.RequestAborted);
                return Ok(result);
            });
        }

        [HttpDelete("{userId:guid}/freeze")]
        public Task<IActionResult> UnfreezeAccount(Guid userId)
        {
            return ExecuteAsync(async () => Ok(await _service.SetAccountFrozen(GetCurrentUserId(), userId, false)));
        }

        private Guid GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var userId)
                ? userId
                : Guid.Empty;
        }
    }
}
