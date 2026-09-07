using Domain.Enums;

namespace Application.DTOs.Property
{
    public record PropertyManagementResponse(
        Guid Id,
        string Title,
        string Address,
        decimal Price,
        decimal OriginalPriceAmount,
        string OriginalPriceCurrency,
        double Area,
        int RoomsCount,
        double Latitude,
        double Longitude,
        PropertyType Type,
        PropertyStatus Status,
        DateTime? SoldAtUtc,
        string? OwnerFullName,
        string? OwnerEmail,
        string? OwnerPhoneNumber,
        Guid? OwnerClientId,
        Guid? ResponsibleRealtorId,
        List<string> PhotoPaths,
        DateTime CreatedDate);
}
