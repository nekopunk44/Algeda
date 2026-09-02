using Application.DTOs.Property;

namespace Application.DTOs.Deal
{
    public record SaleRequestDetailsResponse(
        DealWorkflowResponse Deal,
        PropertyResponse Property,
        SaleRequestLifecycleResponse? Lifecycle);
}
