using API.Auth;
using API.Configuration;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        private const string GenericEmailFlowMessage = "Если учетная запись существует, письмо отправлено.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly UserSessionService _userSessionService;
        private readonly AuthRegistrationService _authRegistrationService;
        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly IApplicationEmailService _applicationEmailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IJwtTokenGenerator tokenGenerator,
            UserSessionService userSessionService,
            AuthRegistrationService authRegistrationService,
            IIdentityAccountManager identityAccountManager,
            IApplicationEmailService applicationEmailService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _tokenGenerator = tokenGenerator;
            _userSessionService = userSessionService;
            _authRegistrationService = authRegistrationService;
            _identityAccountManager = identityAccountManager;
            _applicationEmailService = applicationEmailService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var normalizedEmail = NormalizeEmail(request.Email);
                var user = await _userManager.FindByEmailAsync(normalizedEmail);
                if (user is null)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Не авторизован",
                        Detail = "Неверный email или пароль."
                    });
                }

                if (await _userManager.IsLockedOutAsync(user))
                {
                    return BuildTooManyLoginAttemptsResponse();
                }

                var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
                if (!passwordValid)
                {
                    if (await _userManager.GetLockoutEnabledAsync(user))
                    {
                        await _userManager.AccessFailedAsync(user);
                        if (await _userManager.IsLockedOutAsync(user))
                        {
                            return BuildTooManyLoginAttemptsResponse();
                        }
                    }

                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Не авторизован",
                        Detail = "Неверный email или пароль."
                    });
                }

                if (await _userManager.GetAccessFailedCountAsync(user) > 0)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                }

                if (user.IsFrozen)
                {
                    return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
                    {
                        Status = StatusCodes.Status423Locked,
                        Title = "Аккаунт заморожен",
                        Detail = "Ваш аккаунт заморожен. Обратитесь к администратору."
                    });
                }

                if (!user.EmailConfirmed)
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Подтвердите email",
                        Detail = "Email не подтвержден. Введите код из письма или запросите новый код."
                    });
                }

                var roles = await _userManager.GetRolesAsync(user);
                var roleList = roles.ToArray();
                var deviceName = BuildDeviceName(
                    HttpContext.Request.Headers["X-Client-Device"].ToString(),
                    HttpContext.Request.Headers.UserAgent.ToString());
                var ipAddress = GetClientIpAddress();
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
                var sessionId = await _userSessionService.ResolveLoginSessionId(
                    user.Id,
                    deviceName,
                    ipAddress,
                    userAgent,
                    HttpContext.RequestAborted);
                var token = _tokenGenerator.Generate(user, roleList, sessionId);

                await _userSessionService.RecordLogin(
                    sessionId,
                    user.Id,
                    deviceName,
                    ipAddress,
                    userAgent,
                    token.ExpiresAtUtc,
                    HttpContext.RequestAborted);

                return Ok(new LoginResponse(
                    token.AccessToken,
                    token.ExpiresAtUtc,
                    "Bearer",
                    roleList));
            });
        }

        [Authorize]
        [HttpGet("session-status")]
        public IActionResult SessionStatus()
        {
            return Ok(new AccountStatusResponse(false));
        }

        [Authorize]
        [HttpPost("logout")]
        public Task<IActionResult> Logout()
        {
            return ExecuteAsync(async () =>
            {
                var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdValue = User.FindFirstValue(AuthSessionClaimNames.SessionId);
                if (Guid.TryParse(userIdValue, out var userId)
                    && Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    await _userSessionService.Revoke(
                        userId,
                        sessionId,
                        HttpContext.RequestAborted);
                }

                return NoContent();
            });
        }

        [AllowAnonymous]
        [HttpPost("register/client")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> RegisterClient([FromBody] Application.DTOs.Auth.RegisterClientRequest request)
        {
            return ExecuteAsync(async () =>
                Ok(await _authRegistrationService.RegisterClient(request, HttpContext.RequestAborted)));
        }

        [AllowAnonymous]
        [HttpPost("register/realtor-request")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> RegisterRealtorRequest([FromBody] Application.DTOs.Auth.RegisterRealtorRequest request)
        {
            return ExecuteAsync(async () =>
                Ok(await _authRegistrationService.RegisterRealtorRequest(request, HttpContext.RequestAborted)));
        }

        [AllowAnonymous]
        [HttpPost("confirm-email")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var pendingResult = await _authRegistrationService.ConfirmPendingRegistration(
                    NormalizeEmail(request.Email),
                    request.Code,
                    HttpContext.RequestAborted);

                if (pendingResult is not null)
                {
                    return Ok(new AuthOperationResponse(pendingResult.Message));
                }

                var result = await _identityAccountManager.ConfirmEmailByCode(
                    NormalizeEmail(request.Email),
                    request.Code,
                    HttpContext.RequestAborted);

                if (!result.Succeeded)
                {
                    throw new ValidationException(JoinIdentityErrors(
                        result.Errors,
                        "Код подтверждения неверный или устарел. Запросите новый код."));
                }

                return Ok(new AuthOperationResponse("Email успешно подтвержден."));
            });
        }

        [AllowAnonymous]
        [HttpPost("resend-confirmation")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> ResendConfirmation([FromBody] ResendEmailConfirmationRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var normalizedEmail = NormalizeEmail(request.Email);
                await _authRegistrationService.ResendPendingCode(
                    normalizedEmail,
                    HttpContext.RequestAborted);

                await TrySendEmailConfirmationByEmail(normalizedEmail);
                return Ok(new AuthOperationResponse(GenericEmailFlowMessage));
            });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var normalizedEmail = NormalizeEmail(request.Email);
                var user = await _identityAccountManager.GetUserByEmail(
                    normalizedEmail,
                    HttpContext.RequestAborted);

                if (user is not null && user.EmailConfirmed)
                {
                    var code = await _identityAccountManager.GeneratePasswordResetCode(
                        user.UserId,
                        HttpContext.RequestAborted);

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        await _applicationEmailService.SendPasswordResetAsync(
                            user.Email,
                            code,
                            user.DisplayName,
                            HttpContext.RequestAborted);
                    }
                }

                return Ok(new AuthOperationResponse(GenericEmailFlowMessage));
            });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            return ExecuteAsync(async () =>
            {
                if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
                {
                    throw new ValidationException("Пароль и подтверждение пароля не совпадают.");
                }

                var normalizedEmail = NormalizeEmail(request.Email);
                var user = await _identityAccountManager.GetUserByEmail(
                    normalizedEmail,
                    HttpContext.RequestAborted);
                var result = await _identityAccountManager.ResetPasswordByCode(
                    normalizedEmail,
                    request.Code,
                    request.NewPassword,
                    HttpContext.RequestAborted);

                if (!result.Succeeded)
                {
                    throw new ValidationException(JoinIdentityErrors(
                        result.Errors,
                        "Код восстановления неверный или устарел. Запросите новый код."));
                }

                if (user is not null)
                {
                    await _userSessionService.RevokeAll(
                        user.UserId,
                        cancellationToken: HttpContext.RequestAborted);
                }

                return Ok(new AuthOperationResponse("Пароль успешно изменен."));
            });
        }

        private async Task TrySendEmailConfirmationByEmail(string normalizedEmail)
        {
            try
            {
                var user = await _identityAccountManager.GetUserByEmail(
                    normalizedEmail,
                    HttpContext.RequestAborted);

                if (user is null || user.EmailConfirmed)
                {
                    return;
                }

                var code = await _identityAccountManager.GenerateEmailConfirmationCode(
                    user.UserId,
                    HttpContext.RequestAborted);

                if (string.IsNullOrWhiteSpace(code))
                {
                    return;
                }

                await _applicationEmailService.SendEmailConfirmationAsync(
                    user.Email,
                    code,
                    user.DisplayName,
                    HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось отправить код подтверждения email для {Email}. Пользователь может запросить код повторно.",
                    normalizedEmail);
            }
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private ObjectResult BuildTooManyLoginAttemptsResponse()
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Слишком много попыток входа",
                Detail = "Вход временно заблокирован. Повторите попытку через 15 минут."
            });
        }

        private static string JoinIdentityErrors(IReadOnlyList<string> errors, string fallback)
        {
            return errors.Count == 0
                ? fallback
                : string.Join("; ", errors);
        }

        private string? GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private static string BuildDeviceName(string? explicitDevice, string? userAgent)
        {
            if (!string.IsNullOrWhiteSpace(explicitDevice))
            {
                return BuildDeviceNameFromClientCode(explicitDevice.Trim());
            }

            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "Неизвестное устройство";
            }

            var ua = userAgent;
            var os = ua.Contains("Android", StringComparison.OrdinalIgnoreCase)
                ? "Android"
                : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                    ? "iOS"
                    : ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)
                        ? "Windows"
                        : ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)
                            ? "macOS"
                            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)
                                ? "Linux"
                                : "Устройство";

            var client = ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)
                ? "Edge"
                : ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)
                    ? "Chrome"
                    : ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)
                        ? "Firefox"
                        : ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase)
                            ? "Safari"
                            : ua.Contains("Dart/", StringComparison.OrdinalIgnoreCase)
                                ? "мобильное приложение"
                                : "браузер";

            return $"{os} · {client}";
        }

        private static string BuildDeviceNameFromClientCode(string value)
        {
            var parts = value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .ToArray();

            if (parts.Length >= 2 && parts[0] == "mobile")
            {
                return $"{DisplayOs(parts[1])} · мобильное приложение";
            }

            if (parts.Length >= 3 && parts[0] == "web")
            {
                return $"{DisplayOs(parts[1])} · {DisplayBrowser(parts[2])}";
            }

            return value;
        }

        private static string DisplayOs(string value)
        {
            return value switch
            {
                "windows" => "Windows",
                "macos" => "macOS",
                "android" => "Android",
                "ios" => "iOS",
                "linux" => "Linux",
                _ => "Устройство"
            };
        }

        private static string DisplayBrowser(string value)
        {
            return value switch
            {
                "edge" => "Edge",
                "chrome" => "Chrome",
                "firefox" => "Firefox",
                "safari" => "Safari",
                _ => "браузер"
            };
        }
    }
}
