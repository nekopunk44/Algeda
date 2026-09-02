using API.Auth;
using Application.DTOs.Currency;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/currencies")]
    public class CurrenciesController : ApiControllerBase
    {
        private readonly CurrencyRateService _service;

        public CurrenciesController(CurrencyRateService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery][Range(1, 500)] int limit = 200,
            [FromQuery] bool includeInactive = false)
        {
            return ExecuteAsync(async () =>
            {
                var items = await _service.Get(limit, includeInactive);
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
        public Task<IActionResult> Create([FromBody] CreateCurrencyRateRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdateCurrencyRateRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.Update(id, request)));
        }

        [HttpPatch("{id:guid}/active")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> SetActive(Guid id, [FromBody] SetCurrencyRateActiveRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.SetActive(id, request.IsActive)));
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
