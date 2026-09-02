using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Web.Services
{
    public class TokenDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TokenDelegatingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                var accessToken = await httpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            }

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return BuildSyntheticErrorResponse(
                    request,
                    HttpStatusCode.ServiceUnavailable,
                    "Сервер временно недоступен",
                    "Не удалось связаться с сервером. Попробуйте еще раз позже.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return BuildSyntheticErrorResponse(
                    request,
                    HttpStatusCode.GatewayTimeout,
                    "Сервер отвечает слишком долго",
                    "Сервер не успел ответить. Попробуйте еще раз.");
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return BuildSyntheticErrorResponse(
                    request,
                    HttpStatusCode.RequestTimeout,
                    "Запрос отменен",
                    "Запрос был отменен. Попробуйте еще раз.");
            }
        }

        private static HttpResponseMessage BuildSyntheticErrorResponse(
            HttpRequestMessage request,
            HttpStatusCode statusCode,
            string title,
            string detail)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = JsonContent.Create(new ProblemDetails
                {
                    Status = (int)statusCode,
                    Title = title,
                    Detail = detail
                })
            };

            return response;
        }
    }
}
