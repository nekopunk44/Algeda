using API.Auth;
using Application.DTOs.Property;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/properties")]
    public class PropertiesController : ApiControllerBase
    {
        private readonly PropertyService _service;

        public PropertiesController(PropertyService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.GetForManagement(
                limit,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpGet("available")]
        [AllowAnonymous]
        public Task<IActionResult> GetAvailable()
        {
            return ExecuteAsync(async () =>
            {
                var properties = await _service.GetAvailable();
                return Ok(properties.Select(property => property.WithoutPrivateOwnerData()));
            });
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () =>
                Ok((await _service.GetById(id)).WithoutPrivateOwnerData()));
        }

        [HttpGet("{id:guid}/management")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetByIdForManagement(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                return Ok(await _service.GetByIdForManagement(id));
            });
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Create([FromBody] CreatePropertyRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(
                    request,
                    GetUserEmailOrEmpty(),
                    ShouldAssignCurrentRealtorAsResponsible());
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdatePropertyRequest request)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                var safeRequest = IsAdmin()
                    ? request
                    : request with { ResponsibleRealtorId = null };
                var updated = await _service.Update(safeRequest with { PropertyId = id });
                return Ok(updated);
            });
        }

        [HttpPut("{id:guid}/price")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdatePropertyPriceRequest request)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                var updated = await _service.UpdatePrice(request with { PropertyId = id });
                return Ok(updated);
            });
        }

        [HttpPatch("{id:guid}/mark-as-sold")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> MarkAsSold(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                return Ok(await _service.MarkAsSold(id));
            });
        }

        [HttpPatch("{id:guid}/hide")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Hide(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                return Ok(await _service.Hide(id));
            });
        }

        [HttpPatch("{id:guid}/show")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Show(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                return Ok(await _service.Show(id));
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Delete(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.EnsureCanManage(id, IsAdmin(), GetUserEmailOrEmpty());
                await _service.Delete(id);
                return NoContent();
            });
        }

        private bool ShouldAssignCurrentRealtorAsResponsible()
        {
            return User.IsInRole(AppRoles.Realtor)
                && !User.IsInRole(AppRoles.Admin)
                && !User.IsInRole(AppRoles.SuperAdmin);
        }

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? string.Empty;
        }

        private bool IsAdmin()
        {
            return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
        }
    }
}
