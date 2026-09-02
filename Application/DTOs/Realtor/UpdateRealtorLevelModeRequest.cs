using Domain.Primitives;

namespace Application.DTOs.Realtor
{
    public record UpdateRealtorLevelModeRequest(
        bool IsLevelManuallyAssigned,
        RealtorLevel? Level);
}
