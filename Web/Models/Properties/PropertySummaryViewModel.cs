namespace Web.Models.Properties;

public sealed class PropertySummaryViewModel
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    // Base price used for compatibility and filtering.
    public decimal Price { get; init; }

    public decimal OriginalPriceAmount { get; init; }

    public string OriginalPriceCurrency { get; init; } = "USD";

    public double Area { get; init; }

    public int RoomsCount { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string Type { get; init; } = "Undefined";

    public string Status { get; init; } = "Undefined";

    public DateTime? SoldAtUtc { get; init; }

    public string? OwnerFullName { get; init; }

    public string? OwnerEmail { get; init; }

    public string? OwnerPhoneNumber { get; init; }

    public Guid? ResponsibleRealtorId { get; init; }

    public string? MainPhotoPath { get; init; }

    public IReadOnlyList<string> PhotoPaths { get; init; } = [];

    public IReadOnlyList<PropertyCriterionViewModel> Criteria { get; init; } = [];

    public DateTime CreatedDate { get; init; }
}
