namespace Application.DTOs.ClientRequirement
{
    public record RequirementNotificationHistoryItemResponse(
        Guid PropertyId,
        string Title,
        string Address,
        decimal Price,
        double Area,
        DateTime SentAtUtc,
        string NotificationType);
}
