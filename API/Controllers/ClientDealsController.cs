using API.Auth;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/client-deals")]
    [Authorize(Roles = AppRoles.Client)]
    public class ClientDealsController : ApiControllerBase
    {
        private readonly IClientDealCenterService _service;

        public ClientDealsController(IClientDealCenterService service)
        {
            _service = service;
        }

        [HttpGet("mine")]
        public Task<IActionResult> GetMine(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] DealStatus? status = null,
            [FromQuery] string? scope = "all")
        {
            return ExecuteAsync(async () => Ok(await _service.GetMyDeals(
                GetUserEmailOrEmpty(),
                limit,
                search,
                status,
                scope)));
        }

        [HttpGet("mine/{id:guid}")]
        public Task<IActionResult> GetMineById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetMyDealById(GetUserEmailOrEmpty(), id)));
        }

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }
    }
}
