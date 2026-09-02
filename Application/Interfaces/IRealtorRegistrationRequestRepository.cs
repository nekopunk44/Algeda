using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IRealtorRegistrationRequestRepository : IRepository<RealtorRegistrationRequest>
    {
        Task<RealtorRegistrationRequest?> GetPendingByEmail(string email);
        Task<RealtorRegistrationRequest?> GetLatestApprovedByEmail(string email);
        Task<RealtorRegistrationRequest?> GetLatestApprovedByIdentityUserId(Guid identityUserId);
        Task<List<RealtorRegistrationRequest>> GetApprovedByPhoneNumbers(IReadOnlyCollection<string> phoneNumbers);

        Task<List<RealtorRegistrationRequest>> GetByStatus(
            RealtorRegistrationRequestStatus? status,
            int limit);
    }
}
