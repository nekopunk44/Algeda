using API.Auth;
using Application.DTOs.PropertyCriterionValue;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/properties/{propertyId:guid}/criteria")]
    public class PropertyCriterionValuesController : ApiControllerBase
    {
        private readonly PropertyService _propertyService;

        public PropertyCriterionValuesController(PropertyService propertyService)
        {
            _propertyService = propertyService;
        }

        [HttpGet]
        [AllowAnonymous]
        public Task<IActionResult> Get(Guid propertyId)
        {
            return ExecuteAsync(async () => Ok(await _propertyService.GetCriteria(propertyId)));
        }

        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Upsert(
            Guid propertyId,
            [FromBody] UpsertPropertyCriterionValueRequest request)
        {
            return ExecuteAsync(async () =>
            {
                await _propertyService.EnsureCanManage(propertyId, IsAdmin(), GetUserEmailOrEmpty());
                var value = await _propertyService.UpsertCriterionValue(propertyId, request);
                return Ok(value);
            });
        }

        [HttpDelete("{criterionDefinitionId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Delete(
            Guid propertyId,
            Guid criterionDefinitionId)
        {
            return ExecuteAsync(async () =>
            {
                await _propertyService.EnsureCanManage(propertyId, IsAdmin(), GetUserEmailOrEmpty());
                await _propertyService.RemoveCriterionValue(propertyId, criterionDefinitionId);
                return NoContent();
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
}
