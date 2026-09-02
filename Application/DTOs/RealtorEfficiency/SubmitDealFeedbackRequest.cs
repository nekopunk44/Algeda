using Domain.Enums;

namespace Application.DTOs.RealtorEfficiency
{
    public record SubmitDealFeedbackRequest(
        Guid DealId,
        int ServiceScore,
        FeedbackFormType FormType = FeedbackFormType.Undefined,
        int? CommunicationScore = null,
        int? ResponsivenessScore = null,
        int? ExpertiseScore = null,
        int? TitleAccuracyScore = null,
        int? CriteriaAccuracyScore = null,
        int? DescriptionAccuracyScore = null,
        int? PhotosAccuracyScore = null,
        string? Comment = null);
}
