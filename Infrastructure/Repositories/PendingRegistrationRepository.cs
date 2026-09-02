using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PendingRegistrationRepository
        : Repository<PendingRegistration>, IPendingRegistrationRepository
    {
        private readonly AppDbContext _context;

        public PendingRegistrationRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<PendingRegistration?> GetByEmail(
            string email,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.PendingRegistrations
                .FirstOrDefaultAsync(x => x.Email.ToLower() == normalized, cancellationToken);
        }

        public async Task<PendingRegistration?> GetByPhone(
            string phoneNumber,
            CancellationToken cancellationToken = default)
        {
            var normalized = phoneNumber.Trim();
            return await _context.PendingRegistrations
                .FirstOrDefaultAsync(x => x.PhoneNumber == normalized, cancellationToken);
        }

        public async Task DeleteExpired(
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var expired = await _context.PendingRegistrations
                .Where(x => x.CodeExpiresAtUtc < nowUtc)
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
            {
                return;
            }

            _context.PendingRegistrations.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task Delete(
            PendingRegistration registration,
            CancellationToken cancellationToken = default)
        {
            _context.PendingRegistrations.Remove(registration);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task SaveChanges(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
