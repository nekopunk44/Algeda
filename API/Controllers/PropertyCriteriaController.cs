using API.Auth;
using Application.DTOs.PropertyCriterionDefinition;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/property-criteria")]
    public class PropertyCriteriaController : ApiControllerBase
    {
        private readonly PropertyCriterionDefinitionService _service;

        public PropertyCriteriaController(PropertyCriterionDefinitionService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery][Range(1, 500)] int limit = 200,
            [FromQuery] bool includeHidden = false)
        {
            return ExecuteAsync(async () =>
            {
                var items = await _service.Get(limit, includeHidden);
                return Ok(items);
            });
        }

        [HttpGet("paged")]
        public Task<IActionResult> GetPaged(
            [FromQuery][Range(1, 500)] int page = 1,
            [FromQuery][Range(1, 100)] int pageSize = 20,
            [FromQuery] bool includeHidden = true,
            [FromQuery] string? search = null,
            [FromQuery] string? sort = null)
        {
            return ExecuteAsync(async () =>
            {
                var items = await _service.GetPage(
                    page,
                    pageSize,
                    includeHidden,
                    search,
                    sort);

                return Ok(items);
            });
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Create([FromBody] CreatePropertyCriterionDefinitionRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdatePropertyCriterionDefinitionRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.Update(id, request)));
        }

        [HttpPatch("{id:guid}/hidden")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> SetHidden(
            Guid id,
            [FromBody] SetPropertyCriterionDefinitionHiddenRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.SetHidden(id, request.IsHidden)));
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
    }
}
