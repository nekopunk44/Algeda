using Domain.Entities;

namespace Application.Interfaces
{
    public interface IPendingRegistrationRepository : IRepository<PendingRegistration>
    {
        Task<PendingRegistration?> GetByEmail(string email, CancellationToken cancellationToken = default);

        Task<PendingRegistration?> GetByPhone(string phoneNumber, CancellationToken cancellationToken = default);

        Task DeleteExpired(DateTime nowUtc, CancellationToken cancellationToken = default);

        Task Delete(PendingRegistration registration, CancellationToken cancellationToken = default);

        Task SaveChanges(CancellationToken cancellationToken = default);
    }
}
