using Web.Models.Api;

namespace Web.Models.RealEstate;

public sealed class RealEstateIndexViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public RealEstateFilterViewModel Filters { get; init; } = new();

    public IReadOnlyList<RealEstatePropertyCardViewModel> Items { get; init; } = [];

    public IReadOnlyList<string> AvailableTypes { get; init; } = [];

    public IReadOnlyList<string> AvailableStatuses { get; init; } = [];

    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}

public sealed class RealEstateFilterViewModel
{
    public string? Search { get; init; }

    public string? SellerSearch { get; init; }

    public string? CriterionSearch { get; init; }

    public string? Type { get; init; }

    public string? Status { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string PriceCurrency { get; init; } = "USD";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public class RealEstatePropertyCardViewModel
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    // Base price used in filters/matching compatibility.
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

    public Guid? OwnerClientId { get; init; }

    public string? MainPhotoPath { get; init; }

    public IReadOnlyList<string> PhotoPaths { get; init; } = [];

    public IReadOnlyList<RealEstatePropertyCriterionShortViewModel> Criteria { get; init; } = [];

    public DateTime CreatedDate { get; init; }
}

public sealed class RealEstatePropertyManagementViewModel : RealEstatePropertyCardViewModel
{
}

public sealed class RealEstateEditViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public RealEstatePropertyManagementViewModel? Property { get; init; }

    public IReadOnlyList<RealEstateCriterionDefinitionViewModel> CriterionDefinitions { get; init; } = [];

    public IReadOnlyList<RealEstateCriterionValueViewModel> CurrentCriteria { get; init; } = [];

    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];
}

public sealed class RealEstateCreateViewModel
{
    public ApiErrorViewModel? ApiError { get; init; }

    public RealEstatePropertyFormValues Form { get; init; } = new();

    public IReadOnlyList<RealEstateCriterionDefinitionViewModel> CriterionDefinitions { get; init; } = [];

    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];
}

public sealed class RealEstatePropertyFormValues
{
    public string Title { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public decimal Price { get; init; } = 100000m;

    public string PriceCurrency { get; init; } = "USD";

    public double Area { get; init; } = 60d;

    public int RoomsCount { get; init; } = 2;

    public double Latitude { get; init; } = 47.0105;

    public double Longitude { get; init; } = 28.8638;

    public string Type { get; init; } = "Apartment";

    public string OwnerFullName { get; init; } = string.Empty;

    public string OwnerEmail { get; init; } = string.Empty;

    public string OwnerPhoneNumber { get; init; } = string.Empty;

    public Guid? OwnerClientId { get; init; }
}

public sealed class CurrencyRateViewModel
{
    public Guid Id { get; init; }

    public string Code { get; init; } = "USD";

    public string Name { get; init; } = string.Empty;

    public string Symbol { get; init; } = string.Empty;

    public decimal RateToBase { get; init; }

    public bool IsActive { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class RealEstateCriterionDefinitionViewModel
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValueType { get; init; } = "Undefined";

    public string? Category { get; init; }

    public string? Description { get; init; }

    public bool IsHidden { get; init; }

    public IReadOnlyList<RealEstateCriterionOptionViewModel> Options { get; init; } = [];
}

public sealed class RealEstateCriterionOptionViewModel
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

public sealed class RealEstateCriterionValueViewModel
{
    public Guid CriterionDefinitionId { get; init; }

    public string CriterionCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValueType { get; init; } = "Undefined";

    public string? Category { get; init; }

    public string RawValue { get; init; } = string.Empty;
}

public sealed class RealEstateCriterionSubmitModel
{
    public Guid CriterionDefinitionId { get; init; }

    public string? Value { get; init; }

    public List<string>? Values { get; init; }
}

public sealed class RealEstatePropertyCriterionShortViewModel
{
    public string Code { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Category { get; init; }
}
