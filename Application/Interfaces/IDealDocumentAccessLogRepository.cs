using Application.DTOs.DealDocuments;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IDealDocumentAccessLogRepository : IRepository<DealDocumentAccessLog>
    {
        Task<IReadOnlyList<DealDocumentAccessLogSearchRow>> Search(
            DealDocumentAccessLogFilter filter,
            CancellationToken cancellationToken = default);
    }
}
