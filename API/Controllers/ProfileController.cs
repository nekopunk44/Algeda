using API.Auth;
using Application.DTOs.Profile;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
[ApiController]
[Route("api/profile")]
[Route("api/account")]
public sealed class ProfileController : ApiControllerBase
{
    private readonly UserProfileService _userProfileService;
    private readonly UserSessionService _userSessionService;

    public ProfileController(
        UserProfileService userProfileService,
        UserSessionService userSessionService)
    {
        _userProfileService = userProfileService;
        _userSessionService = userSessionService;
    }

    [HttpGet("me")]
    public Task<IActionResult> GetMe()
    {
        return ExecuteAsync(async () =>
        {
            var profile = await _userProfileService.GetCurrent(
                GetUserEmailOrEmpty(),
                User.IsInRole(AppRoles.Realtor),
                User.IsInRole(AppRoles.Client),
                IsAdmin());
            return Ok(profile);
        });
    }

    [HttpPut("me")]
    public Task<IActionResult> UpdateMe([FromBody] UpdateUserProfileRequest request)
    {
        return ExecuteAsync(async () =>
        {
            var profile = await _userProfileService.UpdateCurrent(
                GetUserEmailOrEmpty(),
                User.IsInRole(AppRoles.Realtor),
                User.IsInRole(AppRoles.Client),
                IsAdmin(),
                request);
            return Ok(profile);
        });
    }

    [HttpPost("change-password")]
    public Task<IActionResult> ChangePassword([FromBody] ChangeMyPasswordRequest request)
    {
        return ExecuteAsync(async () =>
        {
            await _userProfileService.ChangePassword(GetUserEmailOrEmpty(), request);
            return Ok(new { message = "Пароль успешно изменен." });
        });
    }

    [HttpPost("request-email-change")]
    public Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeRequest request)
    {
        return ExecuteAsync(async () =>
        {
            await _userProfileService.RequestEmailChange(
                GetUserEmailOrEmpty(),
                request,
                HttpContext.RequestAborted);

            return Ok(new { message = "Мы отправили код подтверждения на новый email." });
        });
    }

    [HttpPost("confirm-email-change")]
    public Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
    {
        return ExecuteAsync(async () =>
        {
            var profile = await _userProfileService.ConfirmEmailChange(
                GetUserEmailOrEmpty(),
                User.IsInRole(AppRoles.Realtor),
                User.IsInRole(AppRoles.Client),
                IsAdmin(),
                request,
                HttpContext.RequestAborted);

            return Ok(profile);
        });
    }

    [HttpGet("sessions")]
    public Task<IActionResult> GetSessions()
    {
        return ExecuteAsync(async () =>
        {
            var sessions = await _userSessionService.GetActiveSessions(
                GetUserId(),
                GetCurrentSessionId(),
                HttpContext.RequestAborted);

            return Ok(sessions);
        });
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public Task<IActionResult> RevokeSession(Guid sessionId)
    {
        return ExecuteAsync(async () =>
        {
            await _userSessionService.Revoke(
                GetUserId(),
                sessionId,
                HttpContext.RequestAborted);

            return NoContent();
        });
    }

    [HttpPost("avatar")]
    [Authorize(Roles = AppRoles.Realtor)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public Task<IActionResult> UploadAvatar([FromForm(Name = "avatar")] IFormFile? avatar)
    {
        return ExecuteAsync(async () =>
        {
            if (avatar is null || avatar.Length == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Выберите файл аватара."
                });
            }

            await using var stream = avatar.OpenReadStream();
            await using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);

            var path = await _userProfileService.UploadRealtorAvatar(
                GetUserEmailOrEmpty(),
                avatar.FileName,
                avatar.ContentType,
                copy.ToArray());

            return Ok(new { path });
        });
    }

    [HttpDelete("avatar")]
    [Authorize(Roles = AppRoles.Realtor)]
    public Task<IActionResult> DeleteAvatar()
    {
        return ExecuteAsync(async () =>
        {
            await _userProfileService.RemoveRealtorAvatar(GetUserEmailOrEmpty());
            return NoContent();
        });
    }

    private string GetUserEmailOrEmpty()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId)
            ? userId
            : Guid.Empty;
    }

    private bool IsAdmin()
    {
        return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
    }

    private Guid? GetCurrentSessionId()
    {
        var value = User.FindFirstValue(AuthSessionClaimNames.SessionId);
        return Guid.TryParse(value, out var sessionId)
            ? sessionId
            : null;
    }
}
