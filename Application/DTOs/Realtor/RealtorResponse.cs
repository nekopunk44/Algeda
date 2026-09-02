using Domain.Primitives;

namespace Application.DTOs.Realtor
{
    public record RealtorResponse(
        Guid Id,
        string FirstName,
        string LastName,
        string? MiddleName,
        string PhoneNumber,
        double CurrentKpiScore,
        double AverageRating,
        int DealsThisMonth,
        RealtorLevel Level,
        bool IsLevelManuallyAssigned,
        string? AvatarPath,
        DateTime CreatedDate);
}
