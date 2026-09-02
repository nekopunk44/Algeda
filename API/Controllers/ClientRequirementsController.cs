using API.Auth;
using Application.DTOs.ClientRequirement;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [ApiController]
    [Route("api/client-requirements")]
    public class ClientRequirementsController : ApiControllerBase
    {
        private readonly ClientRequirementService _service;

        public ClientRequirementsController(ClientRequirementService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () => Ok(await _service.Get(limit)));
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetById(id)));
        }

        [HttpGet("active/by-client/{clientId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetActiveByClient(Guid clientId)
        {
            return ExecuteAsync(async () =>
            {
                var active = await _service.GetActive(clientId);
                if (active is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Не найдено",
                        Detail = "Активные требования этого клиента не найдены."
                    });
                }

                return Ok(active);
            });
        }

        [HttpGet("me/active")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> GetMyActive()
        {
            return ExecuteAsync(async () =>
            {
                var email = GetUserEmailOrEmpty();
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Требуется авторизация",
                        Detail = "Не удалось определить email пользователя. Выполните вход еще раз."
                    });
                }

                var clientId = await _service.ResolveClientIdByEmail(email);
                var active = await _service.GetActive(clientId);
                if (active is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Не найдено",
                        Detail = "Ваши активные требования не найдены."
                    });
                }

                return Ok(active);
            });
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Create([FromBody] CreateRequirementRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPost("me")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> CreateMy([FromBody] CreateMyRequirementRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var email = GetUserEmailOrEmpty();
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Требуется авторизация",
                        Detail = "Не удалось определить email пользователя. Выполните вход еще раз."
                    });
                }

                var clientId = await _service.ResolveClientIdByEmail(email);
                var created = await _service.Create(new CreateRequirementRequest(
                    clientId,
                    request.DesiredType,
                    request.DesiredTypes,
                    request.Latitude,
                    request.Longitude,
                    request.SearchRadiusMeters,
                    request.IgnoreArea,
                    request.MinPrice,
                    request.MaxPrice,
                    request.MinArea,
                    request.MaxArea,
                    request.AddressQuery,
                    request.MinMatchPercentage,
                    request.PriceWeight,
                    request.AreaWeight,
                    request.Criteria));

                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Update(Guid id, [FromBody] UpdateRequirementRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var updated = await _service.Update(request with { Id = id });
                return Ok(updated);
            });
        }

        [HttpPut("me/{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> UpdateMy(Guid id, [FromBody] UpdateMyRequirementRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var ownershipError = await EnsureCurrentClientOwnsRequirement(id);
                if (ownershipError is not null)
                {
                    return ownershipError;
                }

                var updated = await _service.Update(new UpdateRequirementRequest(
                    id,
                    request.DesiredType,
                    request.DesiredTypes,
                    request.Latitude,
                    request.Longitude,
                    request.SearchRadiusMeters,
                    request.IgnoreArea,
                    request.MinPrice,
                    request.MaxPrice,
                    request.MinArea,
                    request.MaxArea,
                    request.AddressQuery,
                    request.MinMatchPercentage,
                    request.PriceWeight,
                    request.AreaWeight,
                    request.Criteria));

                return Ok(updated);
            });
        }

        [HttpPost("{id:guid}/subscribe")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> Subscribe(Guid id, [FromQuery][Range(1, 20)] int topMatchesLimit = 5)
        {
            return ExecuteAsync(async () =>
            {
                var ownershipError = await EnsureCurrentClientOwnsRequirement(id);
                if (ownershipError is not null)
                {
                    return ownershipError;
                }

                var emailsSent = await _service.Subscribe(id, topMatchesLimit);
                return Ok(new
                {
                    requirementId = id,
                    emailsSent
                });
            });
        }

        [HttpGet("{id:guid}/notifications/history")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> GetNotificationHistory(Guid id, [FromQuery][Range(1, 100)] int limit = 20)
        {
            return ExecuteAsync(async () =>
            {
                var ownershipError = await EnsureCurrentClientOwnsRequirement(id);
                if (ownershipError is not null)
                {
                    return ownershipError;
                }

                var items = await _service.GetNotificationHistory(id, limit);
                return Ok(items);
            });
        }

        [HttpPatch("{id:guid}/deactivate")]
        public Task<IActionResult> Deactivate(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                var ownershipError = await EnsureCurrentClientOwnsRequirement(id);
                if (ownershipError is not null)
                {
                    return ownershipError;
                }

                await _service.Deactivate(id);
                return NoContent();
            });
        }

        [HttpDelete("{id:guid}")]
        public Task<IActionResult> Delete(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                var ownershipError = await EnsureCurrentClientOwnsRequirement(id);
                if (ownershipError is not null)
                {
                    return ownershipError;
                }

                await _service.Delete(id);
                return NoContent();
            });
        }

        private async Task<IActionResult?> EnsureCurrentClientOwnsRequirement(Guid requirementId)
        {
            if (!User.IsInRole(AppRoles.Client))
            {
                return null;
            }

            var email = GetUserEmailOrEmpty();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Требуется авторизация",
                    Detail = "Не удалось определить email пользователя. Выполните вход еще раз."
                });
            }

            var clientId = await _service.ResolveClientIdByEmail(email);
            var existing = await _service.GetById(requirementId);
            if (existing.ClientId != clientId)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Не найдено",
                    Detail = "Требования не найдены."
                });
            }

            return null;
        }

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }
    }
}
