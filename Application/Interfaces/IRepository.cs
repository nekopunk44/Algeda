using Domain.ValueObjects;

namespace Application.Interfaces
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task<TEntity?> GetById(Guid id);

        Task<List<TEntity>> Get(int limit);

        Task<TEntity> Add(TEntity entity);

        void Update(TEntity entity);

        bool Delete(TEntity entity);

    }
}
