using Domain.Entities;

namespace Application.Interfaces;

public interface IUserSessionRepository
{
    Task Add(UserSession session, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSession>> GetActiveByUser(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSession>> GetActiveTrackedByUser(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<UserSession?> GetById(Guid id, CancellationToken cancellationToken = default);

    Task<UserSession?> GetActiveById(
        Guid id,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task RevokeAll(
        Guid userId,
        DateTime revokedAtUtc,
        Guid? exceptSessionId = null,
        CancellationToken cancellationToken = default);

    Task SaveChanges(CancellationToken cancellationToken = default);
}
