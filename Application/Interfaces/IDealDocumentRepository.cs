using Domain.Entities;

namespace Application.Interfaces
{
    public interface IDealDocumentRepository : IRepository<DealDocument>
    {
        Task<IReadOnlyList<DealDocument>> GetActiveByDeal(Guid dealId, CancellationToken cancellationToken = default);
        Task<DealDocument?> GetActiveById(Guid dealId, Guid documentId, CancellationToken cancellationToken = default);
    }
}
