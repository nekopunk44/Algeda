using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly DbContext Context;
        protected readonly DbSet<TEntity> Set;

        public Repository(DbContext context)
        {
            Context = context;
            Set = context.Set<TEntity>();
        }

        public virtual async Task<TEntity?> GetById(Guid id)
        {
            return await Set.FindAsync(id);
        }

        public virtual async Task<List<TEntity>> Get(int limit)
        {
            return await Set
                .AsNoTracking()
                .Take(limit)
                .ToListAsync();
        }

        public virtual async Task<TEntity> Add(TEntity entity)
        {
            await Set.AddAsync(entity);
            await Context.SaveChangesAsync();
            return entity;
        }

        public virtual void Update(TEntity entity)
        {
            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                Set.Update(entity);
            }

            Context.SaveChanges();
        }

        public virtual bool Delete(TEntity entity)
        {
            Set.Remove(entity);
            return Context.SaveChanges() > 0;
        }
    }
}
