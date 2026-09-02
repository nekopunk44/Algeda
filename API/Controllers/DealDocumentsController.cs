using API.Auth;
using Application.DTOs.DealDocuments;
using Application.Services;
using Domain.Primitives;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Authorize(Policy = AuthorizationPolicies.RealtorOrAdmin)]
    public sealed class DealDocumentsController : ApiControllerBase
    {
        private readonly DealDocumentService _service;

        public DealDocumentsController(DealDocumentService service)
        {
            _service = service;
        }

        [HttpGet("api/deals/{dealId:guid}/documents")]
        public Task<IActionResult> GetForDeal(Guid dealId, CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () => Ok(await _service.GetForDeal(
                dealId,
                BuildActorContext(),
                cancellationToken)));
        }

        [HttpPost("api/deals/{dealId:guid}/documents")]
        [RequestSizeLimit(DealDocumentService.MaxFileBytes + 64 * 1024)]
        public Task<IActionResult> Add(
            Guid dealId,
            [FromForm] DealDocumentUploadForm form,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                if (form.File is null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Ошибка валидации",
                        Detail = "Выберите файл документа."
                    });
                }

                await using var stream = form.File.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);

                var result = await _service.Add(
                    dealId,
                    new CreateDealDocumentRequest(
                        form.Title,
                        form.File.FileName,
                        form.File.ContentType,
                        memory.ToArray()),
                    BuildActorContext(),
                    cancellationToken);

                return Ok(result);
            });
        }

        [HttpGet("api/deals/{dealId:guid}/documents/{documentId:guid}/download")]
        public Task<IActionResult> Download(Guid dealId, Guid documentId, CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                var file = await _service.Open(
                    dealId,
                    documentId,
                    BuildActorContext(),
                    cancellationToken);

                return File(file.Content, file.ContentType, file.OriginalFileName);
            });
        }

        [HttpDelete("api/deals/{dealId:guid}/documents/{documentId:guid}")]
        public Task<IActionResult> Delete(Guid dealId, Guid documentId, CancellationToken cancellationToken)
        {
            return ExecuteAsync(async () =>
            {
                await _service.Delete(
                    dealId,
                    documentId,
                    BuildActorContext(),
                    cancellationToken);

                return NoContent();
            });
        }

        [HttpGet("api/deal-documents/access-log")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> AccessLog(
            [FromQuery] DateOnly? dateFrom = null,
            [FromQuery] DateOnly? dateTo = null,
            [FromQuery] DealDocumentAction? action = null,
            [FromQuery] Guid? dealId = null,
            [FromQuery] string? search = null,
            [FromQuery][Range(1, 500)] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(async () => Ok(await _service.GetAccessLog(
                new DealDocumentAccessLogFilter(dateFrom, dateTo, action, dealId, search, limit),
                User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin),
                cancellationToken)));
        }

        private DealDocumentActorContext BuildActorContext()
        {
            return new DealDocumentActorContext(
                TryGetUserId(),
                GetUserEmailOrEmpty(),
                User.Identity?.Name,
                User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin),
                User.IsInRole(AppRoles.Realtor),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }

        private Guid? TryGetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(raw, out var userId) && userId != Guid.Empty
                ? userId
                : null;
        }

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }
    }

    public sealed class DealDocumentUploadForm
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public IFormFile? File { get; set; }
    }
}
