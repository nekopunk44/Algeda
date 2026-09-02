using Application.DTOs.Property;

namespace Application.DTOs.Deal
{
    public record CreateMySaleRequest(
        CreatePropertyRequest Property,
        IReadOnlyCollection<SaleRequestCriterionRequest>? Criteria,
        string? Message);
}
