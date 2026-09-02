using API.Auth;
using Application.DTOs.Review;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    // Client feedback is submitted through the ownership-aware realtor-efficiency flow.
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsController : ApiControllerBase
    {
        private readonly ReviewService _service;

        public ReviewsController(ReviewService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit)));
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpGet("by-realtor/{realtorId:guid}")]
        public Task<IActionResult> GetByRealtor(Guid realtorId)
        {
            return ExecuteAsync(async () => Ok(await _service.GetByRealtor(realtorId)));
        }

        [HttpPost]
        public Task<IActionResult> Create([FromBody] CreateReviewRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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
    }
}
