namespace Application.DTOs.PropertyMatching
{
    public record PropertyMatchingRequest(
        Guid RequirementId,
        int Limit = 20);
}
