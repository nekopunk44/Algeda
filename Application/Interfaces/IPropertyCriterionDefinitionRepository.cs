using Domain.Entities;

namespace Application.Interfaces
{
    public interface IPropertyCriterionDefinitionRepository : IRepository<PropertyCriterionDefinition>
    {
        Task<List<PropertyCriterionDefinition>> Get(int limit, bool includeHidden);
        Task<(List<PropertyCriterionDefinition> Items, int TotalCount)> GetPage(
            int page,
            int pageSize,
            bool includeHidden,
            string? search,
            string? sort);
        Task<PropertyCriterionDefinition?> GetByIdForUpdate(Guid id);
        Task<PropertyCriterionDefinition?> GetByCode(string code);
        Task SaveChangesAsync();
    }
}
