using Domain.Primitives;

namespace Application.DTOs.RealtorActivity
{
    public record CreateActivityLogRequest(
        Guid RealtorId,
        ActivityType Type,
        int Points);
}
