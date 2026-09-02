using Domain.Enums;
using Application.DTOs.PropertyCriterionValue;

namespace Application.DTOs.Property
{
    public record PropertyResponse(
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
        string? MainPhotoPath,
        List<string> PhotoPaths,
        List<PropertyCriterionValueResponse> Criteria,
        DateTime CreatedDate)
    {
        public PropertyResponse WithoutPrivateOwnerData()
        {
            return this with
            {
                OwnerFullName = null,
                OwnerEmail = null,
                OwnerPhoneNumber = null,
                OwnerClientId = null,
                ResponsibleRealtorId = null
            };
        }
    }
}
