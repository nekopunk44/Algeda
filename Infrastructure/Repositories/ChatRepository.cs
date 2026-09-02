using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ChatRepository : Repository<ChatMessage>, IChatRepository
    {
        private readonly AppDbContext _context;

        public ChatRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<List<ChatMessage>> GetDialog(Guid user1, Guid user2)
        {
            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m =>
                    (m.SenderId == user1 && m.ReceiverId == user2)
                    || (m.SenderId == user2 && m.ReceiverId == user1))
                .OrderBy(m => m.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<ChatMessage>> GetDealDialog(Guid dealId, int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 1000);

            return await _context.ChatMessages
                .AsNoTracking()
                .Where(x => x.DealId == dealId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(safeLimit)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();
        }

        public Task<int> CountDealMessages(Guid dealId)
        {
            return _context.ChatMessages.CountAsync(x => x.DealId == dealId);
        }

        public Task<int> CountUnreadByReceiver(Guid receiverId)
        {
            return _context.ChatMessages.CountAsync(x => x.ReceiverId == receiverId && !x.IsRead);
        }

        public async Task<List<ChatMessage>> GetUnreadByReceiver(Guid receiverId, int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 1000);
            return await _context.ChatMessages
                .AsNoTracking()
                .Where(x => x.ReceiverId == receiverId && !x.IsRead && x.DealId.HasValue)
                .OrderByDescending(x => x.CreatedDate)
                .Take(safeLimit)
                .ToListAsync();
        }

        public async Task<int> MarkDealMessagesAsRead(Guid dealId, Guid receiverId)
        {
            var unread = await _context.ChatMessages
                .Where(x => x.DealId == dealId && x.ReceiverId == receiverId && !x.IsRead)
                .ToListAsync();

            if (unread.Count == 0)
            {
                return 0;
            }

            foreach (var message in unread)
            {
                message.MarkAsRead();
            }

            return await _context.SaveChangesAsync();
        }

        public async Task<int> MarkAllMessagesAsRead(Guid receiverId)
        {
            var unread = await _context.ChatMessages
                .Where(x => x.ReceiverId == receiverId && !x.IsRead)
                .ToListAsync();

            if (unread.Count == 0)
            {
                return 0;
            }

            foreach (var message in unread)
            {
                message.MarkAsRead();
            }

            return await _context.SaveChangesAsync();
        }
    }
}
