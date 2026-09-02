using API.Auth;
using Application.DTOs.Complaint;
using Application.Services;
using Domain.Primitives;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/complaints")]
    public class ComplaintsController : ApiControllerBase
    {
        private readonly ComplaintService _service;

        public ComplaintsController(ComplaintService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Get(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] ComplaintStatus? status = null,
            [FromQuery] ComplaintCategory? category = null,
            [FromQuery] bool? dealLinked = null)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit, status, category, dealLinked)));
        }

        [HttpGet("open")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetOpen()
        {
            return ExecuteAsync(async () => Ok(await _service.GetOpen()));
        }

        [HttpGet("my-realtor")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMyRealtorComplaints([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () =>
            {
                var complaints = await _service.GetForCurrentRealtor(GetUserEmailOrEmpty(), limit);
                return Ok(complaints);
            });
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> Create([FromBody] CreateComplaintRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.CreateForCurrentClient(GetUserEmailOrEmpty(), request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPatch("{id:guid}/in-progress")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> MarkInProgress(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.MarkInProgress(id);
                return NoContent();
            });
        }

        [HttpPatch("{id:guid}/opened")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> MarkOpened(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.MarkOpened(id);
                return NoContent();
            });
        }

        [HttpPatch("{id:guid}/resolve")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Resolve(Guid id, [FromBody] ResolveComplaintRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var resolved = await _service.Resolve(request with { ComplaintId = id });
                return Ok(resolved);
            });
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

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }
    }
}
