using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IChatRepository : IRepository<ChatMessage>
    {
        Task<List<ChatMessage>> GetDialog(Guid user1, Guid user2);
        Task<List<ChatMessage>> GetDealDialog(Guid dealId, int limit);
        Task<int> CountDealMessages(Guid dealId);
        Task<int> CountUnreadByReceiver(Guid receiverId);
        Task<List<ChatMessage>> GetUnreadByReceiver(Guid receiverId, int limit);
        Task<int> MarkDealMessagesAsRead(Guid dealId, Guid receiverId);
        Task<int> MarkAllMessagesAsRead(Guid receiverId);
    }
}
