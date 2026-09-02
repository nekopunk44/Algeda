using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ClientRepository : Repository<Client>, IClientRepository
    {
        private readonly AppDbContext _context;

        public ClientRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<Client?> GetByPhone(string phone)
        {
            return await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        }

        public async Task<Client?> GetByEmail(string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            return await _context.Clients
                .AsNoTracking()
                .Where(x => x.Email != null)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync(x => x.Email!.ToLower() == normalizedEmail);
        }
    }
}
