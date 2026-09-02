using Domain.Entities;

namespace Application.Interfaces
{
    public interface IClientRepository : IRepository<Client>
    {
        Task<Client?> GetByPhone(string phone);
        Task<Client?> GetByEmail(string email);
    }
}
