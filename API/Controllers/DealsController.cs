using API.Auth;
using Application.DTOs.Deal;
using Application.Services;
using Domain.Enums;
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
    [Route("api/deals")]
    public class DealsController : ApiControllerBase
    {
        private readonly DealService _service;

        public DealsController(DealService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Get([FromQuery][Range(1, 500)] int limit = 100)
        {
            return ExecuteAsync(async () =>
            {
                if (IsAdmin())
                    return Ok(await _service.Get(limit));

                return Ok(await _service.GetMyDealsForDashboard(
                    GetUserEmailOrEmpty(),
                    limit,
                    search: null,
                    status: null,
                    source: null));
            });
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetWorkflowByIdForCurrentUser(
                id,
                IsAdmin(),
                User.IsInRole(AppRoles.Realtor),
                GetUserEmailOrEmpty())));
        }

        [HttpGet("{id:guid}/workflow")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetWorkflowById(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                var result = await _service.GetWorkflowByIdForCurrentUser(
                    id,
                    IsAdmin(),
                    User.IsInRole(AppRoles.Realtor),
                    GetUserEmailOrEmpty());

                return Ok(result);
            });
        }

        [HttpGet("incoming")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetIncoming(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] DealSource? source = null)
        {
            return ExecuteAsync(async () => Ok(await _service.GetIncomingForDashboard(
                IsAdmin(),
                GetUserEmailOrEmpty(),
                limit,
                search,
                source)));
        }

        [HttpGet("mine")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMine(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] DealStatus? status = null,
            [FromQuery] DealSource? source = null)
        {
            return ExecuteAsync(async () => Ok(await _service.GetMyDealsForDashboard(
                GetUserEmailOrEmpty(),
                limit,
                search,
                status,
                source)));
        }

        [HttpGet("all")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetAll(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] DealStatus? status = null,
            [FromQuery] DealSource? source = null)
        {
            return ExecuteAsync(async () => Ok(await _service.GetAllDealsForAdmin(
                limit,
                search,
                status,
                source)));
        }

        [HttpGet("by-realtor/{realtorId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetByRealtor(Guid realtorId)
        {
            return ExecuteAsync(async () => Ok(await _service.GetByRealtorForCurrentUser(
                realtorId,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpGet("by-client/{clientId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.ClientOrAdmin)]
        public Task<IActionResult> GetByClient(Guid clientId)
        {
            return ExecuteAsync(async () => Ok(await _service.GetByClientForCurrentUser(
                clientId,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> Create([FromBody] CreateDealRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.Create(request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            });
        }

        [HttpPost("me/request")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> CreateMyRequest([FromBody] CreateMyDealRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.CreateMyRequest(GetUserEmailOrEmpty(), request);
                return CreatedAtAction(nameof(GetWorkflowById), new { id = created.Id }, created);
            });
        }

        [HttpGet("me/sale-requests")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> GetMySaleRequests(
            [FromQuery][Range(1, 500)] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] DealStatus? status = null)
        {
            return ExecuteAsync(async () => Ok(await _service.GetMySaleRequests(
                GetUserEmailOrEmpty(),
                limit,
                search,
                status)));
        }

        [HttpGet("me/sale-requests/{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> GetMySaleRequestById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetMySaleRequestById(
                GetUserEmailOrEmpty(),
                id)));
        }

        [HttpGet("{id:guid}/sale-request")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetSaleRequestById(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.GetSaleRequestByIdForCurrentUser(
                id,
                IsAdmin(),
                User.IsInRole(AppRoles.Realtor),
                GetUserEmailOrEmpty())));
        }

        [HttpPost("me/sale-requests")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> CreateMySaleRequest([FromBody] CreateMySaleRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _service.CreateMySaleRequest(GetUserEmailOrEmpty(), request);
                return CreatedAtAction(nameof(GetMySaleRequestById), new { id = created.Deal.Id }, created);
            });
        }

        [HttpPut("me/sale-requests/{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> UpdateMySaleRequest(Guid id, [FromBody] UpdateMySaleRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.UpdateMySaleRequest(
                GetUserEmailOrEmpty(),
                id,
                request)));
        }

        [HttpPut("{id:guid}/sale-request")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> UpdateSaleRequest(Guid id, [FromBody] UpdateMySaleRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.UpdateSaleRequestByRealtorOrAdmin(
                id,
                request,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpPatch("me/sale-requests/{id:guid}/cancel")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> CancelMySaleRequest(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.CancelMySaleRequest(
                GetUserEmailOrEmpty(),
                id)));
        }

        [HttpPatch("{id:guid}/accept")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> AcceptIncoming(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.AcceptIncoming(id, GetUserEmailOrEmpty())));
        }

        [HttpPatch("{id:guid}/reject")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> RejectIncoming(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.RejectIncoming(id, GetUserEmailOrEmpty())));
        }

        [HttpPatch("{id:guid}/release")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> Release(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.ReleaseByRealtor(id, GetUserEmailOrEmpty())));
        }

        [HttpPatch("{id:guid}/assign")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> AssignRealtor(Guid id, [FromBody] AssignDealRealtorRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.AssignRealtor(id, request.RealtorId)));
        }

        [HttpPatch("{id:guid}/publish-property")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> PublishSaleProperty(Guid id)
        {
            return ExecuteAsync(async () => Ok(await _service.PublishSaleProperty(
                id,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpGet("realtors/search")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> SearchRealtors(
            [FromQuery] string? search,
            [FromQuery][Range(1, 100)] int limit = 20)
        {
            return ExecuteAsync(async () => Ok(await _service.SearchRealtorsForAssignment(search, limit)));
        }

        [HttpGet("status-counts")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> GetStatusCounts([FromQuery] DealSource? source = null)
        {
            return ExecuteAsync(async () => Ok(await _service.GetStatusCountsForDashboard(
                IsAdmin(),
                GetUserEmailOrEmpty(),
                source)));
        }

        [HttpPost("{id:guid}/notes")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> AddNote(Guid id, [FromBody] UpsertDealNoteRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.AddNote(
                id,
                request.Text,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpPut("{id:guid}/notes/{noteId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> UpdateNote(Guid id, Guid noteId, [FromBody] UpsertDealNoteRequest request)
        {
            return ExecuteAsync(async () => Ok(await _service.UpdateNote(
                id,
                noteId,
                request.Text,
                IsAdmin(),
                GetUserEmailOrEmpty())));
        }

        [HttpDelete("{id:guid}/notes/{noteId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> DeleteNote(Guid id, Guid noteId)
        {
            return ExecuteAsync(async () =>
            {
                await _service.DeleteNote(
                    id,
                    noteId,
                    IsAdmin(),
                    GetUserEmailOrEmpty());

                return NoContent();
            });
        }

        [HttpPatch("{id:guid}/complete")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Complete(Guid id, [FromBody] CompleteDealRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var completed = await _service.Complete(
                    request with { DealId = id },
                    IsAdmin(),
                    GetUserEmailOrEmpty());
                return Ok(completed);
            });
        }

        [HttpPatch("{id:guid}/cancel")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Cancel(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.Cancel(id, IsAdmin(), GetUserEmailOrEmpty());
                return NoContent();
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
        public Task<IActionResult> Delete(Guid id)
        {
            return ExecuteAsync(async () =>
            {
                await _service.Delete(id, IsAdmin(), GetUserEmailOrEmpty());
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
