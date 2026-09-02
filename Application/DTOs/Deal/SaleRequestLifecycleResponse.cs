using Domain.Enums;

namespace Application.DTOs.Deal
{
    public record SaleRequestLifecycleResponse(
        string Stage,
        string Description,
        SaleRequestBuyerDealResponse? BuyerDeal);

    public record SaleRequestBuyerDealResponse(
        Guid DealId,
        Guid BuyerClientId,
        string BuyerFullName,
        string BuyerPhoneNumber,
        string? BuyerEmail,
        DealStatus Status,
        DateTime CreatedDate);
}
