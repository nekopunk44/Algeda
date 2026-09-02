using Application.Interfaces;
using Domain.Entities;
using Domain.Primitives;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ComplaintRepository : Repository<Complaint>, IComplaintRepository
    {
        private readonly AppDbContext _context;

        public ComplaintRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<List<Complaint>> GetOpenComplaints()
        {
            return await _context.Complaints
                .AsNoTracking()
                .Where(x => x.Status != ComplaintStatus.Resolved)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Complaint>> GetResolvedByRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.Complaints
                .AsNoTracking()
                .Where(x => x.TargetRealtorId == realtorId)
                .Where(x => x.Status == ComplaintStatus.Resolved)
                .Where(x => x.ModerationVerdict == ComplaintReviewVerdict.Confirmed
                         || x.ModerationVerdict == ComplaintReviewVerdict.PartiallyConfirmed)
                .Where(x => x.ResolvedAt != null && x.ResolvedAt >= fromUtc)
                .OrderByDescending(x => x.ResolvedAt)
                .ToListAsync();
        }

        public Task<int> CountByClientAndSubjectBase(Guid clientId, string subjectBase)
        {
            if (string.IsNullOrWhiteSpace(subjectBase))
            {
                return Task.FromResult(0);
            }

            var normalizedBase = subjectBase.Trim();
            var normalizedPrefix = $"{normalizedBase} #";

            return _context.Complaints
                .AsNoTracking()
                .Where(x => x.ClientId == clientId)
                .CountAsync(x => x.Subject == normalizedBase || x.Subject.StartsWith(normalizedPrefix));
        }

        public async Task<List<Complaint>> GetForAdmin(
            int limit,
            ComplaintStatus? status = null,
            ComplaintCategory? category = null,
            bool? dealLinked = null)
        {
            var query = _context.Complaints
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (category.HasValue)
            {
                query = query.Where(x => x.Category == category.Value);
            }

            if (dealLinked.HasValue)
            {
                query = dealLinked.Value
                    ? query.Where(x => x.DealId != null)
                    : query.Where(x => x.DealId == null);
            }

            return await query
                .OrderByDescending(x => x.CreatedDate)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();
        }

        public async Task<List<Complaint>> GetForRealtor(Guid realtorId, int limit)
        {
            return await _context.Complaints
                .AsNoTracking()
                .Where(x => x.TargetRealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();
        }
    }
}
