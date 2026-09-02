using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Web.Models.Api;
using Web.Models.Profile;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public sealed class ProfileController : Controller
{
    private const string SuccessKey = "ProfileSuccess";
    private const string ErrorKey = "ProfileError";
    private const string PendingEmailChangeKey = "PendingEmailChange";
    private const string ShowEmailChangeKey = "ShowEmailChange";
    private const string AuthSuccessKey = "AuthSuccessMessage";

    private readonly ProfileApiClient _profileApiClient;
    private readonly ComplaintsApiClient _complaintsApiClient;

    public ProfileController(ProfileApiClient profileApiClient, ComplaintsApiClient complaintsApiClient)
    {
        _profileApiClient = profileApiClient;
        _complaintsApiClient = complaintsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var profileResult = await _profileApiClient.GetMyProfileAsync(cancellationToken);
        if (profileResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        var profile = profileResult.Data;
        var complaints = Array.Empty<ProfileComplaintViewModel>();
        var sessions = Array.Empty<AccountSessionViewModel>();
        ApiErrorViewModel? apiError = profileResult.Error ?? GetTempApiError();

        if (profile is not null)
        {
            var sessionsResult = await _profileApiClient.GetMySessionsAsync(cancellationToken);
            if (sessionsResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            sessions = (sessionsResult.Data ?? []).ToArray();
            apiError ??= sessionsResult.Error;
        }

        if (profile?.IsRealtor == true)
        {
            var complaintsResult = await _complaintsApiClient.GetMyRealtorComplaintsAsync(200, cancellationToken);
            if (complaintsResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            complaints = (complaintsResult.Data ?? []).ToArray();
            apiError ??= complaintsResult.Error;
        }

        return View(BuildPageViewModel(profile, complaints, sessions, apiError));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "EditForm")] ProfileEditFormViewModel form, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = "Проверьте корректность данных аккаунта.";
            return RedirectToAction(nameof(Index));
        }

        var currentEmail = GetCurrentEmail();
        var requestedEmail = form.Email.Trim();
        if (!string.IsNullOrWhiteSpace(currentEmail)
            && !string.Equals(currentEmail, requestedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailChangeResult = await _profileApiClient.RequestEmailChangeAsync(
                new RequestEmailChangeRequest { NewEmail = requestedEmail },
                cancellationToken);

            if (emailChangeResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            if (emailChangeResult.IsSuccess)
            {
                TempData[SuccessKey] = "Мы отправили код подтверждения на новый email.";
                TempData[PendingEmailChangeKey] = requestedEmail;
                TempData[ShowEmailChangeKey] = true;
            }
            else
            {
                TempData[ErrorKey] = emailChangeResult.Error?.Message ?? "Не удалось отправить код подтверждения email.";
            }

            return RedirectToAction(nameof(Index));
        }

        var request = new UpdateProfileRequest
        {
            Email = requestedEmail,
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(form.MiddleName) ? null : form.MiddleName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber.Trim()
        };

        var result = await _profileApiClient.UpdateMyProfileAsync(request, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Аккаунт обновлен.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось обновить аккаунт.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmailChange(
        [Bind(Prefix = "EmailChangeForm")] ConfirmEmailChangeFormViewModel form,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = "Введите корректный код подтверждения email.";
            TempData[PendingEmailChangeKey] = form.NewEmail;
            TempData[ShowEmailChangeKey] = true;
            return RedirectToAction(nameof(Index));
        }

        var result = await _profileApiClient.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest
        {
            NewEmail = form.NewEmail.Trim(),
            Code = form.Code.Trim()
        }, cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (!result.IsSuccess)
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось подтвердить новый email.";
            TempData[PendingEmailChangeKey] = form.NewEmail.Trim();
            TempData[ShowEmailChangeKey] = true;
            return RedirectToAction(nameof(Index));
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData[AuthSuccessKey] = "Email изменен. Войдите снова с новым адресом.";
        return RedirectToAction("Login", "Auth");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "PasswordForm")] ChangePasswordFormViewModel form, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = "Проверьте корректность данных для смены пароля.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _profileApiClient.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = form.CurrentPassword,
            NewPassword = form.NewPassword,
            ConfirmPassword = form.ConfirmPassword
        }, cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Пароль успешно изменен.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось изменить пароль.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Realtor")]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatar, CancellationToken cancellationToken = default)
    {
        if (avatar is null || avatar.Length == 0)
        {
            TempData[ErrorKey] = "Выберите файл аватара.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _profileApiClient.UploadAvatarAsync(avatar, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Аватар обновлен.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось загрузить аватар.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Realtor")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken = default)
    {
        var result = await _profileApiClient.RemoveAvatarAsync(cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Аватар удален. Установлено изображение по умолчанию.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось удалить аватар.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _profileApiClient.RevokeSessionAsync(id, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            if (await IsCurrentSession(id))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Auth");
            }

            TempData[SuccessKey] = "Подключение завершено.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось завершить подключение.";
        }

        return RedirectToAction(nameof(Index));
    }

    private ProfilePageViewModel BuildPageViewModel(
        ProfileDetailsViewModel? profile,
        IReadOnlyList<ProfileComplaintViewModel> complaints,
        IReadOnlyList<AccountSessionViewModel> sessions,
        ApiErrorViewModel? apiError)
    {
        var editForm = new ProfileEditFormViewModel();
        if (profile is not null)
        {
            editForm = new ProfileEditFormViewModel
            {
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                MiddleName = profile.MiddleName,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber
            };
        }

        return new ProfilePageViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Profile = profile,
            EditForm = editForm,
            EmailChangeForm = new ConfirmEmailChangeFormViewModel
            {
                NewEmail = TempData[PendingEmailChangeKey]?.ToString() ?? string.Empty
            },
            ShowEmailChangeConfirmation = bool.TryParse(TempData[ShowEmailChangeKey]?.ToString(), out var showEmailChange)
                && showEmailChange,
            RealtorComplaints = complaints,
            Sessions = sessions,
            PasswordForm = new ChangePasswordFormViewModel()
        };
    }

    private ApiErrorViewModel? GetTempApiError()
    {
        var message = TempData[ErrorKey]?.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return ApiErrorFactory.Create(StatusCodes.Status400BadRequest, message);
    }

    private IActionResult RedirectToLoginCurrent()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl });
    }

    private string? GetCurrentEmail()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? User.Identity?.Name;
    }

    private async Task<bool> IsCurrentSession(Guid sessionId)
    {
        var claimValue = User.FindFirst("sessionId")?.Value;
        if (Guid.TryParse(claimValue, out var currentSessionId))
        {
            return currentSessionId == sessionId;
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        try
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            claimValue = jwtToken.Claims.FirstOrDefault(x => x.Type == "sessionId")?.Value;
            return Guid.TryParse(claimValue, out currentSessionId)
                && currentSessionId == sessionId;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
