using Domain.Primitives;

namespace Application.DTOs.RealtorActivity
{
    public record UpdateActivityLogRequest(
        ActivityType Type,
        int Points);
}
