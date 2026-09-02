using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IReviewRepository : IRepository<Review>
    {
        Task<Review?> GetByDeal(Guid dealId);
        Task<List<Review>> GetByRealtor(Guid realtorId);
        Task<List<Review>> GetByRealtor(Guid realtorId, DateTime fromUtc);
    }
}
