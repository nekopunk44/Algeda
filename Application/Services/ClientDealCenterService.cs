using Application.DTOs.Deal;
using Application.Interfaces;
using Domain.Enums;

namespace Application.Services
{
    public sealed class ClientDealCenterService : IClientDealCenterService
    {
        private readonly DealService _dealService;

        public ClientDealCenterService(DealService dealService)
        {
            _dealService = dealService;
        }

        public Task<List<DealWorkflowResponse>> GetMyDeals(
            string email,
            int limit,
            string? search,
            DealStatus? status,
            string? scope)
        {
            return _dealService.GetMyDealsForClientCenter(email, limit, search, status, scope);
        }

        public Task<DealWorkflowResponse> GetMyDealById(string email, Guid dealId)
        {
            return _dealService.GetMyDealByIdForClientCenter(email, dealId);
        }
    }
}
