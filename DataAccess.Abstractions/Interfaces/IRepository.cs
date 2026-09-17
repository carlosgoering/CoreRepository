using DataAccess.Abstractions.Models;

namespace DataAccess.Abstractions.Interfaces;

public interface IRepository<TEntity>
    where TEntity : class
{
    Task<TEntity> InsertAsync(TEntity entity);

    Task<TEntity> UpdateAsync(TEntity entity);

    Task DeleteAsync(TEntity entity);

    Task<TEntity?> FirstOrDefaultAsync(Query<TEntity> query);

    Task<PagedResult<TEntity>> SelectPagedAsync(Query<TEntity> query);

    Task<long> CountAsync(Query<TEntity>? query = null);

    Task<bool> ExistsAsync(Query<TEntity> query);
    Task<IReadOnlyCollection<TEntity>> SelectAsync(Query<TEntity> query);
}