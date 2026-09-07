using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IRealtorRepository : IRepository<Realtor>
    {
        Task<Realtor?> GetByPhone(string phone);
        Task<List<Realtor>> GetTopRealtors(int count);
        Task<List<Realtor>> GetActive();
    }
}
