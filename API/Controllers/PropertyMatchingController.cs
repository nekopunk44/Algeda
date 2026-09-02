using API.Auth;
using Application.DTOs.PropertyMatching;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/property-matching")]
    public class PropertyMatchingController : ApiControllerBase
    {
        private readonly PropertyMatchingService _service;

        public PropertyMatchingController(PropertyMatchingService service)
        {
            _service = service;
        }

        [HttpPost("matches")]
        public Task<IActionResult> FindMatches([FromBody] PropertyMatchingRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.FindMatches(request)));
        }

        [HttpPost("preview")]
        public Task<IActionResult> FindPreviewMatches([FromBody] PropertyMatchingPreviewRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.FindMatchesPreview(request)));
        }

        [HttpGet("properties/{propertyId:guid}/subscribed-clients")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetSubscribedClients(
            Guid propertyId,
            [FromQuery][Range(1, 5000)] int requirementsLimit = 500)
        {
            return ExecuteAsync(async () =>
            {
                var clientIds = await _service.FindSubscribedClientsForProperty(propertyId, requirementsLimit);
                return Ok(clientIds);
            });
        }
    }
}
