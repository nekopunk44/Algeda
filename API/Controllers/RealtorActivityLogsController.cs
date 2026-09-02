using API.Auth;
using Application.Exceptions;
using Application.DTOs.RealtorActivity;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
    [ApiController]
    [Route("api/realtor-activity-logs")]
    public class RealtorActivityLogsController : ApiControllerBase
    {
        private readonly RealtorActivityLogService _service;
        private readonly DealService _dealService;

        public RealtorActivityLogsController(
            RealtorActivityLogService service,
            DealService dealService)
        {
            _service = service;
            _dealService = dealService;
        }

        [HttpGet]
        public Task<IActionResult> Get([FromQuery][Range(1, 1000)] int limit = 100)
        {
            return ExecuteAsync(async () =>
            {
                if (IsAdmin())
                    return Ok(await _service.Get(limit));

                var realtorId = await ResolveCurrentRealtorId();
                return Ok(await _service.GetByRealtor(realtorId));
            });
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                var item = await _service.GetById(id);
                if (!IsAdmin() && item.RealtorId != await ResolveCurrentRealtorId())
                    throw new NotFoundException("Запись активности не найдена.");

                return Ok(item);
            });
        }

        [HttpGet("by-realtor/{realtorId:guid}")]
        public Task<IActionResult> GetByRealtor(Guid realtorId)
        {
            return ExecuteAsync(async () =>
            {
                if (!IsAdmin() && realtorId != await ResolveCurrentRealtorId())
                    throw new NotFoundException("Риелтор не найден.");

                return Ok(await _service.GetByRealtor(realtorId));
            });
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Create([FromBody] CreateActivityLogRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityLogRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.Update(id, request)));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Delete(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.Delete(id);
                return NoContent();
            });
        }

        private Task<Guid> ResolveCurrentRealtorId()
        {
            return _dealService.ResolveRealtorIdByEmail(GetUserEmailOrEmpty());
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
}
