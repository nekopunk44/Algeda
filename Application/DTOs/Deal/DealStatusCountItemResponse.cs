using Domain.Enums;

namespace Application.DTOs.Deal
{
    public record DealStatusCountItemResponse(
        DealStatus Status,
        int Count);
}
