using Application.DTOs.Property;

namespace Application.DTOs.Deal
{
    public record UpdateMySaleRequest(
        CreatePropertyRequest Property,
        IReadOnlyCollection<SaleRequestCriterionRequest>? Criteria,
        string? Message);
}
