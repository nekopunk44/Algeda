using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IDealRepository : IRepository<Deal>
    {
        /// <summary>
        /// Executes a state-changing workflow while holding database-wide advisory locks
        /// for the deal and its property. All repository writes made by the callback are
        /// committed atomically or rolled back together.
        /// </summary>
        Task<TResult> ExecuteWorkflow<TResult>(Guid dealId, Func<Task<TResult>> operation);

        Task<Deal?> GetByIdWithNotes(Guid id);
        Task<List<Deal>> GetIncoming(int limit);
        Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit);
        Task<List<Deal>> GetByRealtor(Guid realtorId);
        Task<List<Deal>> GetByClient(Guid clientId);
        Task<List<Deal>> GetByProperty(Guid propertyId);
        Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId);
        Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc);
        Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId);
        Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text);
        Task<bool> DeleteNote(Guid dealId, Guid noteId);
    }
}
