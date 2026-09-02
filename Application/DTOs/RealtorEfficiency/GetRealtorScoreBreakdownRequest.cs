namespace Application.DTOs.RealtorEfficiency
{
    public record GetRealtorScoreBreakdownRequest(
        Guid RealtorId,
        int Limit = 20);
}
