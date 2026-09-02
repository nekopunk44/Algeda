using Domain.Enums;

namespace Application.DTOs.RealtorEfficiency
{
    public record DealFeedbackStateResponse(
        Guid DealId,
        FeedbackFormType FormType,
        bool IsCompleted,
        bool IsSubmitted,
        bool CanSubmit,
        DateTime? CompletedAtUtc,
        DateTime? DeadlineUtc,
        int? DaysRemaining,
        string? BlockReasonCode,
        string? BlockReason);
}
