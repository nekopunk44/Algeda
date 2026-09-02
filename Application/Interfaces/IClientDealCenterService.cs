using Application.DTOs.Deal;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IClientDealCenterService
    {
        Task<List<DealWorkflowResponse>> GetMyDeals(
            string email,
            int limit,
            string? search,
            DealStatus? status,
            string? scope);

        Task<DealWorkflowResponse> GetMyDealById(string email, Guid dealId);
    }
}
