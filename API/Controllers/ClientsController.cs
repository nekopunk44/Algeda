using API.Auth;
using Application.DTOs.Client;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/clients")]
    public class ClientsController : ApiControllerBase
    {
        private readonly ClientService _service;

        public ClientsController(ClientService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit)));
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost]
        public Task<IActionResult> Create([FromBody] CreateClientRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPut("{id:guid}")]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var updated = await _service.Update(request with { Id = id });
                return Ok(updated);
            });
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpDelete("{id:guid}")]
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
