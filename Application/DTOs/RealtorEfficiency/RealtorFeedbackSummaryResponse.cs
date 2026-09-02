namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorFeedbackSummaryResponse(
        Guid RealtorId,
        int TotalFeedbackCount,
        int ServiceFeedbackCount,
        int PropertyFeedbackCount,
        int PurchaseFeedbackCount,
        int SaleFeedbackCount,
        double? AverageServiceScore,
        double? AverageCommunicationScore,
        double? AverageResponsivenessScore,
        double? AverageExpertiseScore,
        double? AverageTitleAccuracyScore,
        double? AverageDescriptionAccuracyScore,
        double? AveragePhotosAccuracyScore,
        double? AverageCriteriaAccuracyScore,
        IReadOnlyList<RealtorFeedbackSummaryItemResponse> RecentItems);

    public record RealtorFeedbackSummaryItemResponse(
        Guid FeedbackId,
        Guid DealId,
        Domain.Enums.FeedbackFormType FormType,
        bool IsServiceFeedback,
        bool IsPropertyFeedback,
        int ServiceScore,
        int? CommunicationScore,
        int? ResponsivenessScore,
        int? ExpertiseScore,
        int? TitleAccuracyScore,
        int? DescriptionAccuracyScore,
        int? PhotosAccuracyScore,
        int? CriteriaAccuracyScore,
        string? Comment,
        DateTime CreatedDate);
}
