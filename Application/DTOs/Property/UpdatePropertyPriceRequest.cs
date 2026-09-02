namespace Application.DTOs.Property
{
    public record UpdatePropertyPriceRequest(
        Guid PropertyId,
        decimal NewPrice);
}
