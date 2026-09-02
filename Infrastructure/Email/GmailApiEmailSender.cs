using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

public sealed class GmailApiEmailSender(
    IOptions<GmailApiOptions> options,
    ILogger<GmailApiEmailSender> logger) : IEmailSender
{
    private static readonly Uri TokenEndpoint = new("https://oauth2.googleapis.com/token");
    private static readonly Uri SendEndpoint = new("https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GmailApiOptions _options = options.Value;
    private readonly ILogger<GmailApiEmailSender> _logger = logger;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ValidateMessage(message);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        var accessToken = await GetAccessTokenAsync(httpClient, cancellationToken);
        var rawMessage = BuildRawMessage(message);

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
        {
            Content = JsonContent.Create(new GmailSendRequest(rawMessage), options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await ReadSafeResponseBodyAsync(response, cancellationToken);
        _logger.LogWarning(
            "Gmail API send failed. StatusCode={StatusCode}; Reason={Reason}; Body={Body}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            responseBody);

        throw new InvalidOperationException("Не удалось отправить email через Gmail API.");
    }

    private async Task<string> GetAccessTokenAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = _options.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

        using var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        var responseBody = await ReadSafeResponseBodyAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gmail OAuth token request failed. StatusCode={StatusCode}; Reason={Reason}; Body={Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                responseBody);

            throw new InvalidOperationException("Не удалось получить access token для Gmail API.");
        }

        var token = JsonSerializer.Deserialize<GmailTokenResponse>(responseBody, JsonOptions);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            _logger.LogWarning("Gmail OAuth token response did not include an access token.");
            throw new InvalidOperationException("Gmail API вернул пустой access token.");
        }

        return token.AccessToken;
    }

    private string BuildRawMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        foreach (var recipient in message.To.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mimeMessage.To.Add(MailboxAddress.Parse(recipient));
        }

        mimeMessage.Subject = message.Subject.Trim();
        mimeMessage.Body = message.IsBodyHtml
            ? new TextPart("html") { Text = message.Body }
            : new TextPart("plain") { Text = message.Body };

        using var stream = new MemoryStream();
        mimeMessage.WriteTo(stream);

        return Base64UrlEncode(stream.ToArray());
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static async Task<string> ReadSafeResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return body.Length <= 4000 ? body : body[..4000];
    }

    private static void ValidateMessage(EmailMessage message)
    {
        if (message is null)
        {
            throw new ValidationException("Не передано email-сообщение.");
        }

        if (message.To is null || message.To.Count == 0)
        {
            throw new ValidationException("Не указан получатель email.");
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ValidationException("Не указана тема email.");
        }

        if (string.IsNullOrWhiteSpace(message.Body))
        {
            throw new ValidationException("Не указано тело email.");
        }

        foreach (var recipient in message.To)
        {
            if (!MailAddress.TryCreate(recipient, out _))
            {
                throw new ValidationException($"Некорректный получатель email: '{recipient}'.");
            }
        }
    }

    private sealed record GmailSendRequest(string Raw);

    private sealed record GmailTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);
}
