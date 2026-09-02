using System.Text;
using Application.DTOs.PropertyMatching;
using Application.Email;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public sealed class ApplicationEmailService(IEmailSender emailSender) : IApplicationEmailService
{
    private readonly IEmailSender _emailSender = emailSender;

    public Task SendEmailConfirmationAsync(
        string recipientEmail,
        string confirmationCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default)
    {
        var safeRecipientEmail = recipientEmail.Trim();
        var greetingName = BuildGreetingName(recipientName);
        var safeCode = confirmationCode.Trim();

        var body = $"""
            Здравствуйте, {greetingName}!

            Для подтверждения email в RealEstate Agency введите этот код:

            {safeCode}

            Код действует 15 минут. Если вы не создавали аккаунт, просто проигнорируйте это письмо.

            С уважением,
            RealEstate Agency
            """;

        return _emailSender.SendAsync(
            new EmailMessage(
                [safeRecipientEmail],
                "Код подтверждения email в RealEstate Agency",
                body,
                false),
            cancellationToken);
    }

    public Task SendEmailChangeConfirmationAsync(
        string recipientEmail,
        string confirmationCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default)
    {
        var safeRecipientEmail = recipientEmail.Trim();
        var greetingName = BuildGreetingName(recipientName);
        var safeCode = confirmationCode.Trim();

        var body = $"""
            Здравствуйте, {greetingName}!

            Для смены email в RealEstate Agency введите этот код:

            {safeCode}

            Код действует 15 минут. Если вы не меняли email, просто проигнорируйте это письмо.

            С уважением,
            RealEstate Agency
            """;

        return _emailSender.SendAsync(
            new EmailMessage(
                [safeRecipientEmail],
                "Код подтверждения смены email в RealEstate Agency",
                body,
                false),
            cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string recipientEmail,
        string resetCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default)
    {
        var safeRecipientEmail = recipientEmail.Trim();
        var greetingName = BuildGreetingName(recipientName);
        var safeCode = resetCode.Trim();

        var body = $"""
            Здравствуйте, {greetingName}!

            Для восстановления доступа к аккаунту RealEstate Agency введите этот код:

            {safeCode}

            Код действует 15 минут. Если вы не запрашивали восстановление доступа, просто проигнорируйте это письмо.

            С уважением,
            RealEstate Agency
            """;

        return _emailSender.SendAsync(
            new EmailMessage(
                [safeRecipientEmail],
                "Код восстановления доступа в RealEstate Agency",
                body,
                false),
            cancellationToken);
    }

    public Task SendRequirementTopMatchesAsync(
        string recipientEmail,
        ClientRequirement requirement,
        IReadOnlyList<MatchedPropertyResponse> matches,
        CancellationToken cancellationToken = default)
    {
        var body = BuildTopMatchesBody(requirement, matches);

        return _emailSender.SendAsync(
            new EmailMessage(
                [recipientEmail.Trim()],
                $"Найдены подходящие варианты недвижимости. Новых вариантов: {matches.Count}.",
                body,
                false),
            cancellationToken);
    }

    public Task SendNewRelevantPropertyAsync(
        string recipientEmail,
        PropertySubscriberMatch match,
        CancellationToken cancellationToken = default)
    {
        var body = BuildNewPropertyBody(match);

        return _emailSender.SendAsync(
            new EmailMessage(
                [recipientEmail.Trim()],
                $"Новый подходящий объект: {match.PropertyTitle}",
                body,
                false),
            cancellationToken);
    }

    private static string BuildTopMatchesBody(
        ClientRequirement requirement,
        IReadOnlyList<MatchedPropertyResponse> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Здравствуйте!");
        sb.AppendLine();
        sb.AppendLine($"Для вашего запроса {requirement.Id} найдены новые подходящие объекты:");
        sb.AppendLine();

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            sb.AppendLine($"{i + 1}. {match.Title}");
            sb.AppendLine($"Цена: {match.Price:N0}");
            sb.AppendLine($"Площадь: {match.Area:N1} м2");
            sb.AppendLine($"Адрес: {match.Address}");
            sb.AppendLine($"Совпадение: {match.MatchScore:P1}");
            sb.AppendLine($"Расстояние: {match.DistanceMeters:N0} м");
            sb.AppendLine();
        }

        sb.AppendLine("С уважением, агентство недвижимости");
        return sb.ToString();
    }

    private static string BuildNewPropertyBody(PropertySubscriberMatch match)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Здравствуйте!");
        sb.AppendLine();
        sb.AppendLine("Появился новый объект, подходящий под ваш активный запрос:");
        sb.AppendLine();
        sb.AppendLine($"Название: {match.PropertyTitle}");
        sb.AppendLine($"Цена: {match.PropertyPrice:N0}");
        sb.AppendLine($"Площадь: {match.PropertyArea:N1} м2");
        sb.AppendLine($"Адрес: {match.PropertyAddress}");
        sb.AppendLine($"Совпадение: {match.MatchScore:P1}");
        sb.AppendLine($"Расстояние: {match.DistanceMeters:N0} м");
        sb.AppendLine();
        sb.AppendLine("С уважением, агентство недвижимости");
        return sb.ToString();
    }

    private static string BuildGreetingName(string? recipientName)
    {
        return string.IsNullOrWhiteSpace(recipientName)
            ? "пользователь"
            : recipientName.Trim();
    }
}
