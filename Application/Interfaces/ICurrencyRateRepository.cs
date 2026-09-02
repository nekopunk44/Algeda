using Domain.Entities;

namespace Application.Interfaces
{
    public interface ICurrencyRateRepository : IRepository<CurrencyRate>
    {
        Task<List<CurrencyRate>> Get(int limit, bool includeInactive);
        Task<CurrencyRate?> GetByCode(string code);
        Task<CurrencyRate?> GetByIdForUpdate(Guid id);
        Task SaveChangesAsync();
    }
}
