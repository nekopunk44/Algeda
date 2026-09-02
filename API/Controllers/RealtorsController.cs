using API.Auth;
using Application.DTOs.Realtor;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/realtors")]
    public class RealtorsController : ApiControllerBase
    {
        private readonly RealtorService _service;

        public RealtorsController(RealtorService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit)));
        }

        [HttpGet("top")]
        public Task<IActionResult> GetTop([FromQuery][Range(1, 100)] int count = 10)
        {
            return ExecuteAsync(async () => Ok(await _service.GetTop(count)));
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Create([FromBody] CreateRealtorRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdateRealtorRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var updated = await _service.Update(request with { Id = id });
                return Ok(updated);
            });
        }

        [HttpPatch("{id:guid}/level-mode")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> UpdateLevelMode(Guid id, [FromBody] UpdateRealtorLevelModeRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.UpdateLevelMode(id, request)));
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
