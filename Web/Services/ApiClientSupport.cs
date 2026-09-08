using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.Auth;

namespace Web.Services;

internal static class ApiClientSupport
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal static ApiErrorViewModel BuildUnavailableApiError()
    {
        return ApiErrorFactory.Create(
            StatusCodes.Status503ServiceUnavailable,
            "Не удалось связаться с сервером. Попробуйте еще раз позже.");
    }

    internal static ApiErrorViewModel BuildTimeoutApiError()
    {
        return ApiErrorFactory.Create(
            StatusCodes.Status504GatewayTimeout,
            "Сервер отвечает слишком долго. Попробуйте еще раз.");
    }

    internal static ApiErrorViewModel BuildEmptyPayloadError(string message)
    {
        return ApiErrorFactory.Create(
            StatusCodes.Status500InternalServerError,
            message);
    }

    internal static async Task<ApiErrorViewModel> BuildErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? detail = null;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("detail", out var detailElement)
                    && detailElement.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(detailElement.GetString()))
                {
                    detail = NormalizeApiMessage(detailElement.GetString());
                }

                if (string.IsNullOrWhiteSpace(detail)
                    && root.TryGetProperty("title", out var titleElement)
                    && titleElement.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(titleElement.GetString()))
                {
                    detail = NormalizeApiMessage(titleElement.GetString());
                }

                if (root.TryGetProperty("errors", out var errorsElement)
                    && errorsElement.ValueKind == JsonValueKind.Object)
                {
                    var lines = new List<string>();
                    foreach (var property in errorsElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var message = NormalizeApiMessage(item.GetString());
                            if (string.IsNullOrWhiteSpace(message))
                            {
                                continue;
                            }

                            lines.Add(message);

                            if (lines.Count >= 6)
                            {
                                break;
                            }
                        }

                        if (lines.Count >= 6)
                        {
                            break;
                        }
                    }

                    if (lines.Count > 0)
                    {
                        detail = string.Join(" | ", lines);
                    }
                }
            }
        }
        catch (NotSupportedException)
        {
            // ignored
        }
        catch (JsonException)
        {
            // ignored
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            try
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                detail = IsSafePlainTextError(response, text) ? NormalizeApiMessage(text) : null;
            }
            catch
            {
                // ignored
            }
        }

        return ApiErrorFactory.Create((int)response.StatusCode, detail);
    }

    private static string? NormalizeApiMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var value = message.Trim();
        return value switch
        {
            _ when IsUnsafeErrorPayload(value) => null,
            _ when value.Contains("One or more validation errors occurred", StringComparison.OrdinalIgnoreCase)
                => "Проверьте заполнение формы.",
            _ when value.Contains("The JSON value could not be converted", StringComparison.OrdinalIgnoreCase)
                => "Проверьте формат данных в форме.",
            _ when value.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
                => "Проверьте формат данных в форме.",
            _ when value.Contains("The field", StringComparison.OrdinalIgnoreCase)
                && value.Contains("is required", StringComparison.OrdinalIgnoreCase)
                => "Заполните обязательные поля.",
            _ when value.Contains("The value", StringComparison.OrdinalIgnoreCase)
                && value.Contains("is invalid", StringComparison.OrdinalIgnoreCase)
                => "Проверьте формат введенных данных.",
            _ when value.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
                => "Сервер отвечает слишком долго. Попробуйте еще раз.",
            _ when value.Contains("task was canceled", StringComparison.OrdinalIgnoreCase)
                => "Сервер отвечает слишком долго. Попробуйте еще раз.",
            _ => value
        };
    }

    private static bool IsSafePlainTextError(HttpResponseMessage response, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (IsTransientGatewayStatus((int)response.StatusCode))
        {
            return false;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType)
            && (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var value = text.Trim();
        return value.Length <= 500 && !IsUnsafeErrorPayload(value);
    }

    private static bool IsTransientGatewayStatus(int statusCode)
    {
        return statusCode is StatusCodes.Status502BadGateway
            or StatusCodes.Status503ServiceUnavailable
            or StatusCodes.Status504GatewayTimeout;
    }

    private static bool IsUnsafeErrorPayload(string value)
    {
        return value.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || value.Contains("<head", StringComparison.OrdinalIgnoreCase)
            || value.Contains("<body", StringComparison.OrdinalIgnoreCase)
            || value.Contains("</html", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Render", StringComparison.OrdinalIgnoreCase)
            || value.Contains("nginx", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<ApiOperationResult> SendAsync(
        HttpClient httpClient,
        HttpMethod method,
        string relativeUrl,
        object? payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, relativeUrl);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string? message = null;
                try
                {
                    var operation = await response.Content.ReadFromJsonAsync<AuthOperationResponse>(
                        JsonOptions,
                        cancellationToken);
                    message = operation?.Message;
                }
                catch (JsonException)
                {
                }
                catch (NotSupportedException)
                {
                }

                return ApiOperationResult.Success(message);
            }

            return ApiOperationResult.Failure(await BuildErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiOperationResult.Failure(BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiOperationResult.Failure(BuildTimeoutApiError());
        }
    }
}
