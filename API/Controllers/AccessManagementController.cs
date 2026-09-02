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

        public AccessManagementController(UserAccessManagementService service)
        {
            _service = service;
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
            return ExecuteAsync(async () => Ok(await _service.AssignRole(GetCurrentUserId(), userId, role)));
        }

        [HttpDelete("{userId:guid}/roles/{role}")]
        public Task<IActionResult> RemoveRole(Guid userId, string role)
        {
            return ExecuteAsync(async () => Ok(await _service.RemoveRole(GetCurrentUserId(), userId, role)));
        }

        [HttpPost("{userId:guid}/transfer-super-admin")]
        public Task<IActionResult> TransferSuperAdmin(Guid userId, [FromBody] TransferSuperAdminRequest request)
        {
            var normalizedRequest = request with { TargetUserId = userId };
            return ExecuteAsync(async () => Ok(await _service.TransferSuperAdmin(GetCurrentUserId(), normalizedRequest)));
        }

        [HttpPost("{userId:guid}/freeze")]
        public Task<IActionResult> FreezeAccount(Guid userId)
        {
            return ExecuteAsync(async () => Ok(await _service.SetAccountFrozen(GetCurrentUserId(), userId, true)));
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
