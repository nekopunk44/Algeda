using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using Web.Models.Api;
using Web.Models.Auth;

namespace Web.Services
{
    public sealed record LoginApiResult(LoginResponse? Response, ApiErrorViewModel? Error)
    {
        public bool IsSuccess => Response is not null && Error is null;
    }

    public sealed record AuthFlowApiResult(string? Message, ApiErrorViewModel? Error)
    {
        public bool IsSuccess => Error is null;
    }

    public class AuthApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<LoginApiResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
                {
                    Content = JsonContent.Create(request)
                };
                AddClientHeaders(httpRequest);

                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
                        if (payload is not null && !string.IsNullOrWhiteSpace(payload.AccessToken))
                        {
                            return new LoginApiResult(payload, null);
                        }
                    }
                    catch (JsonException)
                    {
                        // ignored below
                    }
                    catch (NotSupportedException)
                    {
                        // ignored below
                    }

                    return new LoginApiResult(
                        null,
                        ApiErrorFactory.Create(
                            StatusCodes.Status500InternalServerError,
                            "API вернул пустой или некорректный ответ авторизации."));
                }

                return new LoginApiResult(
                    null,
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }
            catch (HttpRequestException)
            {
                return new LoginApiResult(null, ApiClientSupport.BuildUnavailableApiError());
            }
            catch (TaskCanceledException)
            {
                return new LoginApiResult(null, ApiClientSupport.BuildTimeoutApiError());
            }
        }

        public Task<ApiOperationResult> RegisterClientAsync(
            RegisterClientRequest request,
            CancellationToken cancellationToken = default)
        {
            return ApiClientSupport.SendAsync(
                _httpClient,
                HttpMethod.Post,
                "api/auth/register/client",
                request,
                cancellationToken);
        }

        public Task<ApiOperationResult> RegisterRealtorRequestAsync(
            RegisterRealtorRequest request,
            CancellationToken cancellationToken = default)
        {
            return ApiClientSupport.SendAsync(
                _httpClient,
                HttpMethod.Post,
                "api/auth/register/realtor-request",
                request,
                cancellationToken);
        }

        public Task<AuthFlowApiResult> ConfirmEmailAsync(
            ConfirmEmailRequest request,
            CancellationToken cancellationToken = default)
        {
            return SendAuthFlowAsync("api/auth/confirm-email", request, cancellationToken);
        }

        public Task<AuthFlowApiResult> ResendConfirmationAsync(
            ResendEmailConfirmationRequest request,
            CancellationToken cancellationToken = default)
        {
            return SendAuthFlowAsync("api/auth/resend-confirmation", request, cancellationToken);
        }

        public Task<AuthFlowApiResult> ForgotPasswordAsync(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            return SendAuthFlowAsync("api/auth/forgot-password", request, cancellationToken);
        }

        public Task<AuthFlowApiResult> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            return SendAuthFlowAsync("api/auth/reset-password", request, cancellationToken);
        }

        private async Task<AuthFlowApiResult> SendAuthFlowAsync(
            string url,
            object payload,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var operation = await response.Content.ReadFromJsonAsync<AuthOperationResponse>(
                            cancellationToken: cancellationToken);
                        return new AuthFlowApiResult(operation?.Message, null);
                    }
                    catch (JsonException)
                    {
                        return new AuthFlowApiResult(null, null);
                    }
                    catch (NotSupportedException)
                    {
                        return new AuthFlowApiResult(null, null);
                    }
                }

                return new AuthFlowApiResult(
                    null,
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }
            catch (HttpRequestException)
            {
                return new AuthFlowApiResult(null, ApiClientSupport.BuildUnavailableApiError());
            }
            catch (TaskCanceledException)
            {
                return new AuthFlowApiResult(null, ApiClientSupport.BuildTimeoutApiError());
            }
        }

        private void AddClientHeaders(HttpRequestMessage request)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                request.Headers.TryAddWithoutValidation("X-Client-Device", "Web");
                return;
            }

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            request.Headers.TryAddWithoutValidation("X-Client-Device", BuildWebDeviceCode(userAgent));

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            var ipAddress = GetClientIpAddress(httpContext);
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", ipAddress);
            }
        }

        private static string? GetClientIpAddress(HttpContext httpContext)
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(IsPublicAddressCandidate);
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].ToString();
            if (IsPublicAddressCandidate(realIp))
            {
                return realIp;
            }

            var remoteIp = httpContext.Connection.RemoteIpAddress;
            return remoteIp is null || IPAddress.IsLoopback(remoteIp)
                ? null
                : remoteIp.ToString();
        }

        private static bool IsPublicAddressCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return IPAddress.TryParse(value, out var address)
                ? !IPAddress.IsLoopback(address)
                : !string.Equals(value, "::1", StringComparison.Ordinal)
                    && !string.Equals(value, "127.0.0.1", StringComparison.Ordinal);
        }

        private static string BuildWebDeviceCode(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "web;unknown;browser";
            }

            var os = userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)
                ? "windows"
                : userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)
                    ? "macos"
                    : userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
                        ? "android"
                        : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
                            ? "ios"
                            : "unknown";

            var browser = userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)
                ? "edge"
                : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)
                    ? "chrome"
                    : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)
                        ? "firefox"
                        : userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)
                            ? "safari"
                            : "browser";

            return $"web;{os};{browser}";
        }
    }
}
