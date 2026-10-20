using DataAccess.Abstractions.Models;
using System.Linq.Expressions;

namespace DataAccess.Abstractions.Interfaces;

public interface IDataAccessContext<TEntity>
   where TEntity : class
{
    Task InsertAsync(TEntity entity);

    Task UpdateAsync(TEntity entity);

    Task DeleteAsync(TEntity entity);

    Task<TEntity?> FirstOrDefaultAsync(Query<TEntity> query);

    Task<IReadOnlyCollection<TEntity>> SelectAsync(Query<TEntity> query);

    Task<PagedResult<TEntity>> SelectPagedAsync(Query<TEntity> query);

    Task<long> CountAsync(Query<TEntity>? query = null);

    Task<bool> ExistsAsync(Query<TEntity> query);
}
