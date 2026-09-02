using API.Auth;
using Application.DTOs.Auth;
using Application.Services;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ApiController]
    [Route("api/realtor-registration-requests")]
    public class RealtorRegistrationRequestsController : ApiControllerBase
    {
        private readonly RealtorRegistrationRequestService _service;

        public RealtorRegistrationRequestsController(RealtorRegistrationRequestService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery] RealtorRegistrationRequestStatus? status,
            [FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(status, limit)));
        }

        [HttpGet("{id:guid}")]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpPatch("{id:guid}/approve")]
        public Task<IActionResult> Approve(
            Guid id,
            [FromBody] ReviewRealtorRegistrationRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.Approve(id, request.Comment)));
        }

        [HttpPatch("{id:guid}/reject")]
        public Task<IActionResult> Reject(
            Guid id,
            [FromBody] ReviewRealtorRegistrationRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.Reject(id, request.Comment)));
        }
    }
}
