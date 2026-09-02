using Web.Models.Api;
using Web.Models.DealRequests;
using Web.Models.RealEstate;

namespace Web.Models.SellProperty;

public sealed class SellPropertyIndexViewModel
{
    public string? SuccessMessage { get; init; }
    public ApiErrorViewModel? ApiError { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<DealRequestListItemViewModel> Items { get; init; } = [];
}

public sealed class SellPropertyFormViewModel
{
    public string? SuccessMessage { get; init; }
    public ApiErrorViewModel? ApiError { get; init; }
    public Guid? DealId { get; init; }
    public string DealStatus { get; init; } = "Created";
    public bool CanEdit { get; init; } = true;
    public bool IsManagedMode { get; init; }
    public string SubmitController { get; init; } = "SellProperty";
    public string SubmitAction { get; init; } = "Create";
    public string SubmitButtonText { get; init; } = "Отправить заявку";
    public string BackController { get; init; } = "SellProperty";
    public string BackAction { get; init; } = "Index";
    public string? ReturnScope { get; init; }
    public string? ReturnSource { get; init; }
    public string? ReturnSearch { get; init; }
    public string? ReturnStatus { get; init; }
    public int ReturnPage { get; init; } = 1;
    public int ReturnPageSize { get; init; } = 20;
    public string? Comment { get; init; }
    public SaleRequestLifecycleViewModel? Lifecycle { get; init; }
    public RealEstatePropertyFormValues Form { get; init; } = new();
    public IReadOnlyList<string> ExistingPhotoPaths { get; init; } = [];
    public IReadOnlyList<RealEstateCriterionDefinitionViewModel> CriterionDefinitions { get; init; } = [];
    public IReadOnlyList<RealEstateCriterionSubmitModel> InitialCriteria { get; init; } = [];
    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];
}

public sealed class SaleRequestDetailsViewModel
{
    public DealRequestListItemViewModel Deal { get; init; } = new();
    public SaleRequestPropertyViewModel Property { get; init; } = new();
    public SaleRequestLifecycleViewModel? Lifecycle { get; init; }
}

public sealed class SaleRequestPropertyViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal OriginalPriceAmount { get; init; }
    public string OriginalPriceCurrency { get; init; } = "USD";
    public double Area { get; init; }
    public int RoomsCount { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Type { get; init; } = "Undefined";
    public string Status { get; init; } = "Undefined";
    public string? OwnerFullName { get; init; }
    public string? OwnerEmail { get; init; }
    public string? OwnerPhoneNumber { get; init; }
    public IReadOnlyList<string> PhotoPaths { get; init; } = [];
    public IReadOnlyList<SaleRequestPropertyCriterionViewModel> Criteria { get; init; } = [];
}

public sealed class SaleRequestPropertyCriterionViewModel
{
    public Guid CriterionDefinitionId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ValueType { get; init; } = "Undefined";
    public string RawValue { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
}

public sealed class SaleRequestLifecycleViewModel
{
    public string Stage { get; init; } = "Submitted";
    public string Description { get; init; } = string.Empty;
    public SaleRequestBuyerDealViewModel? BuyerDeal { get; init; }
}

public sealed class SaleRequestBuyerDealViewModel
{
    public Guid DealId { get; init; }
    public Guid BuyerClientId { get; init; }
    public string BuyerFullName { get; init; } = string.Empty;
    public string BuyerPhoneNumber { get; init; } = string.Empty;
    public string? BuyerEmail { get; init; }
    public string Status { get; init; } = "Undefined";
    public DateTime CreatedDate { get; init; }
}
