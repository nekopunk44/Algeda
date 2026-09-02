using Domain.Enums;

namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorFeedbackResponse(
        Guid Id,
        Guid DealId,
        Guid ClientId,
        Guid ServiceRealtorId,
        Guid? PropertyId,
        Guid? PropertyResponsibleRealtorId,
        int ServiceScore,
        FeedbackFormType FormType,
        int? CommunicationScore,
        int? ResponsivenessScore,
        int? ExpertiseScore,
        int? TitleAccuracyScore,
        int? CriteriaAccuracyScore,
        int? DescriptionAccuracyScore,
        int? PhotosAccuracyScore,
        string? Comment,
        DateTime CreatedDate);
}
