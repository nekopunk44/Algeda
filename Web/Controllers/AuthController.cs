using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Web.Models;
using Web.Models.Api;
using Web.Models.Auth;
using Web.Services;

namespace Web.Controllers
{
    public class AuthController : Controller
    {
        private const string AuthSuccessKey = "AuthSuccessMessage";
        private const string GenericEmailFlowMessage = "Если учетная запись существует, письмо отправлено.";

        private readonly AuthApiClient _authApiClient;

        public AuthController(AuthApiClient authApiClient)
        {
            _authApiClient = authApiClient;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/", bool registrationHint = false)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RegistrationHint"] = registrationHint;
            ViewData["AuthSuccess"] = TempData[AuthSuccessKey]?.ToString();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginRequest request,
            string returnUrl = "/",
            bool registrationHint = false)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RegistrationHint"] = registrationHint;
            ViewData["AuthSuccess"] = TempData[AuthSuccessKey]?.ToString();

            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var loginResult = await _authApiClient.LoginAsync(request);
            if (!loginResult.IsSuccess || loginResult.Response is null)
            {
                if (IsServiceUnavailable(loginResult.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(loginResult.Error));
                }

                if (loginResult.Error?.StatusCode == StatusCodes.Status423Locked)
                {
                    return RedirectToAction(nameof(Frozen));
                }

                if (string.Equals(loginResult.Error?.Title, "Подтвердите email", StringComparison.OrdinalIgnoreCase))
                {
                    ViewData["ConfirmationEmail"] = request.Email.Trim();
                }

                ViewData["ApiError"] = loginResult.Error ?? ApiErrorFactory.Create(500);
                return View(request);
            }

            var response = loginResult.Response;

            JwtSecurityToken jwtToken;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                jwtToken = handler.ReadJwtToken(response.AccessToken);
            }
            catch (ArgumentException)
            {
                ViewData["ApiError"] = ApiErrorFactory.Create(500, "Не удалось прочитать токен авторизации.");
                return View(request);
            }

            var claims = new List<Claim>();

            var nameIdentifier = jwtToken.Claims.FirstOrDefault(x =>
                x.Type == ClaimTypes.NameIdentifier
                || x.Type == JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.IsNullOrWhiteSpace(nameIdentifier))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
            }

            var email = jwtToken.Claims.FirstOrDefault(x =>
                x.Type == ClaimTypes.Email
                || x.Type == JwtRegisteredClaimNames.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
                claims.Add(new Claim(ClaimTypes.Name, email));
            }

            var sessionId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sessionId")?.Value;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                claims.Add(new Claim("sessionId", sessionId));
            }

            var tokenRoles = jwtToken.Claims
                .Where(x => x.Type == ClaimTypes.Role || x.Type == "role")
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var responseRoles = (response.Roles ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var role in tokenRoles.Concat(responseRoles).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var tokenExpiresUtc = jwtToken.ValidTo == DateTime.MinValue
                ? response.ExpiresAtUtc
                : jwtToken.ValidTo;

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = tokenExpiresUtc.ToUniversalTime()
            };

            authProperties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = response.AccessToken }
            });

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _authApiClient.LogoutAsync(HttpContext.RequestAborted);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Frozen()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RegisterClient()
        {
            return View(new RegisterClientRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterClient(RegisterClientRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.RegisterClientAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            TempData[AuthSuccessKey] = result.Message
                ?? "Мы отправили код подтверждения email. Введите его, чтобы завершить регистрацию.";
            return RedirectToAction(nameof(ConfirmEmail), new { email = request.Email.Trim() });
        }

        [HttpGet]
        public IActionResult RegisterRealtor()
        {
            return View(new RegisterRealtorRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterRealtor(RegisterRealtorRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.RegisterRealtorRequestAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            TempData[AuthSuccessKey] = result.Message
                ?? "Мы отправили код подтверждения email. После ввода кода заявка риелтора будет отправлена администратору.";
            return RedirectToAction(nameof(ConfirmEmail), new { email = request.Email.Trim() });
        }

        [HttpGet]
        public IActionResult RegisterRealtorSubmitted()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ConfirmEmail(string? email = null)
        {
            ViewData["AuthSuccess"] = TempData[AuthSuccessKey]?.ToString();
            return View(new ConfirmEmailRequest
            {
                Email = email?.Trim() ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.ConfirmEmailAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            ViewData["AuthSuccess"] = result.Message ?? "Email успешно подтвержден.";
            return View(request);
        }

        [HttpGet]
        public IActionResult ResendConfirmation(string? email = null)
        {
            return View(new ResendEmailConfirmationRequest
            {
                Email = email?.Trim() ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmation(ResendEmailConfirmationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.ResendConfirmationAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            TempData[AuthSuccessKey] = result.Message ?? GenericEmailFlowMessage;
            return RedirectToAction(nameof(ConfirmEmail), new { email = request.Email.Trim() });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.ForgotPasswordAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            TempData[AuthSuccessKey] = result.Message ?? GenericEmailFlowMessage;
            return RedirectToAction(nameof(ResetPassword), new { email = request.Email.Trim() });
        }

        [HttpGet]
        public IActionResult ResetPassword(string? email = null)
        {
            ViewData["AuthSuccess"] = TempData[AuthSuccessKey]?.ToString();
            return View(new ResetPasswordRequest
            {
                Email = email?.Trim() ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var result = await _authApiClient.ResetPasswordAsync(request);
            if (!result.IsSuccess)
            {
                if (IsServiceUnavailable(result.Error))
                {
                    return View("Error", BuildTemporaryUnavailableError(result.Error));
                }

                ViewData["ApiError"] = result.Error;
                return View(request);
            }

            TempData[AuthSuccessKey] = result.Message ?? "Пароль успешно изменен. Войдите с новым паролем.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private static bool IsServiceUnavailable(ApiErrorViewModel? error)
        {
            return error?.StatusCode is StatusCodes.Status503ServiceUnavailable
                or StatusCodes.Status504GatewayTimeout;
        }

        private ErrorViewModel BuildTemporaryUnavailableError(ApiErrorViewModel? error)
        {
            return new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
                Title = error?.Title ?? "Сервис временно недоступен",
                Heading = "Данные временно недоступны.",
                Message = error?.Message ?? "Попробуйте обновить страницу позже."
            };
        }
    }
}
