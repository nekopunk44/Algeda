using System.Net.Mail;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using MailKit.Security;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

public sealed class MailKitEmailSender(
    IOptions<EmailOptions> options,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;
    private readonly ILogger<MailKitEmailSender> _logger = logger;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ValidateMessage(message);

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Отправка email отключена. Сообщение только залогировано. To={To}; Subject={Subject}",
                string.Join(", ", message.To),
                message.Subject);
            return;
        }

        var mailMessage = new MimeMessage();
        mailMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        foreach (var recipient in message.To.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mailMessage.To.Add(MailboxAddress.Parse(recipient));
        }

        mailMessage.Subject = message.Subject.Trim();
        mailMessage.Body = message.IsBodyHtml
            ? new TextPart("html") { Text = message.Body }
            : new TextPart("plain") { Text = message.Body };

        using var smtpClient = new MailKitSmtpClient();
        var secureSocketOption = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await smtpClient.ConnectAsync(
            _options.Host,
            _options.Port,
            secureSocketOption,
            cancellationToken);

        await smtpClient.AuthenticateAsync(
            _options.User,
            _options.Password,
            cancellationToken);

        await smtpClient.SendAsync(mailMessage, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
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
}
