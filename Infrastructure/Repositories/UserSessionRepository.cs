using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly AppDbContext _dbContext;

    public UserSessionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task Add(UserSession session, CancellationToken cancellationToken = default)
    {
        _dbContext.UserSessions.Add(session);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<UserSession>> GetActiveByUser(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSessions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSession>> GetActiveTrackedByUser(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSessions
            .Where(x => x.UserId == userId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<UserSession?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserSessions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<UserSession?> GetActiveById(
        Guid id,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserSessions
            .FirstOrDefaultAsync(x => x.Id == id
                && x.UserId == userId
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > nowUtc,
                cancellationToken);
    }

    public Task SaveChanges(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
