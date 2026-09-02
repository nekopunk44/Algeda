using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IClientRequirementRepository : IRepository<ClientRequirement>
    {
        Task<ClientRequirement?> GetActiveByClient(Guid clientId);
        Task<List<ClientRequirement>> GetActive(int limit);
    }
}
