using Application.DTOs.PropertyMatching;
using Domain.Entities;

namespace Application.Interfaces;

public interface IApplicationEmailService
{
    Task SendEmailConfirmationAsync(
        string recipientEmail,
        string confirmationCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default);

    Task SendEmailChangeConfirmationAsync(
        string recipientEmail,
        string confirmationCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string recipientEmail,
        string resetCode,
        string? recipientName = null,
        CancellationToken cancellationToken = default);

    Task SendRequirementTopMatchesAsync(
        string recipientEmail,
        ClientRequirement requirement,
        IReadOnlyList<MatchedPropertyResponse> matches,
        CancellationToken cancellationToken = default);

    Task SendNewRelevantPropertyAsync(
        string recipientEmail,
        PropertySubscriberMatch match,
        CancellationToken cancellationToken = default);
}
