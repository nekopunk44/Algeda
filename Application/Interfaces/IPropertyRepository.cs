using Application.DTOs.PropertyMatching;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IPropertyRepository : IRepository<Property>
    {
        Task<List<Property>> GetAvailable();
        Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc);
        Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids);
        Task<Property?> GetByIdForUpdateWithCriteria(Guid id);
        void AddCriterionValue(PropertyCriterionValue value);

        // Жесткие условия подбора применяются на уровне SQL.
        Task<List<PropertyMatchCandidate>> GetCandidatesForRequirement(
            ClientRequirement requirement,
            int take);
    }
}
