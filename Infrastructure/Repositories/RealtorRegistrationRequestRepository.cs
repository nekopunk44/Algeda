using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RealtorRegistrationRequestRepository
        : Repository<RealtorRegistrationRequest>, IRealtorRegistrationRequestRepository
    {
        private readonly AppDbContext _context;

        public RealtorRegistrationRequestRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<RealtorRegistrationRequest?> GetPendingByEmail(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.RealtorRegistrationRequests
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync(x =>
                    x.Status == RealtorRegistrationRequestStatus.Pending
                    && x.Email.ToLower() == normalized);
        }

        public async Task<RealtorRegistrationRequest?> GetLatestApprovedByEmail(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.RealtorRegistrationRequests
                .AsNoTracking()
                .Where(x => x.Status == RealtorRegistrationRequestStatus.Approved)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync(x => x.Email.ToLower() == normalized);
        }

        public async Task<RealtorRegistrationRequest?> GetLatestApprovedByIdentityUserId(Guid identityUserId)
        {
            return await _context.RealtorRegistrationRequests
                .AsNoTracking()
                .Where(x => x.Status == RealtorRegistrationRequestStatus.Approved)
                .Where(x => x.IdentityUserId == identityUserId)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<RealtorRegistrationRequest>> GetApprovedByPhoneNumbers(IReadOnlyCollection<string> phoneNumbers)
        {
            if (phoneNumbers.Count == 0)
            {
                return [];
            }

            return await _context.RealtorRegistrationRequests
                .AsNoTracking()
                .Where(x => x.Status == RealtorRegistrationRequestStatus.Approved)
                .Where(x => phoneNumbers.Contains(x.PhoneNumber))
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<RealtorRegistrationRequest>> GetByStatus(
            RealtorRegistrationRequestStatus? status,
            int limit)
        {
            var query = _context.RealtorRegistrationRequests
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue && status.Value != RealtorRegistrationRequestStatus.Undefined)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            return await query
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }
    }
}
