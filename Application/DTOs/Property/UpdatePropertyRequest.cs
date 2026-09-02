using Domain.Enums;

namespace Application.DTOs.Property
{
    public record UpdatePropertyRequest(
        Guid PropertyId,
        string Title,
        string Address,
        decimal Price,
        string PriceCurrency,
        double Area,
        int RoomsCount,
        double Latitude,
        double Longitude,
        PropertyType Type,
        IReadOnlyCollection<string>? PhotoPaths,
        string? OwnerFullName,
        string? OwnerEmail,
        string? OwnerPhoneNumber,
        Guid? OwnerClientId = null,
        Guid? ResponsibleRealtorId = null);
}
