namespace Application.DTOs.PropertyMatching
{
    public record PropertySubscriberMatch(
        Guid RequirementId,
        Guid ClientId,
        Guid PropertyId,
        string PropertyTitle,
        decimal PropertyPrice,
        double PropertyArea,
        string PropertyAddress,
        double MatchScore,
        double DistanceMeters);
}
