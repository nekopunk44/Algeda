namespace Application.DTOs.Deal
{
    public record CreateDealRequest(
        Guid PropertyId,
        Guid ClientId,
        Guid RealtorId);
}
