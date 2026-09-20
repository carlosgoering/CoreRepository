using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Cache.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Core.Repository;

using System.Text.Json;

public class DataRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, new()
{
    private readonly IDataAccessContext<TEntity> dataContext;
    private readonly ICacheProvider? cache;

    public DataRepository(
        IDataAccessContext<TEntity> dataContext,
        IServiceProvider serviceProvider)
    {
        this.dataContext = dataContext;
        cache = serviceProvider.GetService<ICacheProvider>();
    }

    public async Task<TEntity> InsertAsync(TEntity entity)
    {
        await dataContext.InsertAsync(entity);

        await InvalidateCacheAsync();

        return entity;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity)
    {
        await dataContext.UpdateAsync(entity);

        await InvalidateCacheAsync();

        return entity;
    }

    public async Task DeleteAsync(TEntity entity)
    {
        await dataContext.DeleteAsync(entity);

        await InvalidateCacheAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Query<TEntity> query)
    {
        if (cache is null)
            return await dataContext.FirstOrDefaultAsync(query);

        var key = BuildCacheKey(nameof(FirstOrDefaultAsync), query);

        var cached = await cache.GetAsync<TEntity>(key);

        if (cached is not null)
            return cached;

        var result = await dataContext.FirstOrDefaultAsync(query);

        if (result is not null)
            await cache.SetAsync(key, result);

        return result;
    }

    public async Task<PagedResult<TEntity>> SelectPagedAsync(
        Query<TEntity> query)
    {
        if (cache is null)
            return await dataContext.SelectPagedAsync(query);

        var key = BuildCacheKey(nameof(SelectPagedAsync), query);

        var cached = await cache.GetAsync<PagedResult<TEntity>>(key);

        if (cached is not null)
            return cached;

        var result = await dataContext.SelectPagedAsync(query);

        await cache.SetAsync(key, result);

        return result;
    }

    public async Task<long> CountAsync(
        Query<TEntity>? query = null)
    {
        if (cache is null)
            return await dataContext.CountAsync(query);

        var key = BuildCacheKey(nameof(CountAsync), query);

        var cached = await cache.GetAsync<long?>(key);

        if (cached.HasValue)
            return cached.Value;

        var result = await dataContext.CountAsync(query);

        await cache.SetAsync(key, result);

        return result;
    }

    public async Task<bool> ExistsAsync(Query<TEntity> query)
    {
        if (cache is null)
            return await dataContext.ExistsAsync(query);

        var key = BuildCacheKey(nameof(ExistsAsync), query);

        var cached = await cache.GetAsync<bool?>(key);

        if (cached.HasValue)
            return cached.Value;

        var result = await dataContext.ExistsAsync(query);

        await cache.SetAsync(key, result);

        return result;
    }

    public async Task<IReadOnlyCollection<TEntity>> SelectAsync(
        Query<TEntity> query)
    {
        if (cache is null)
            return await dataContext.SelectAsync(query);

        var key = BuildCacheKey(nameof(SelectAsync), query);

        var cached =
            await cache.GetAsync<IReadOnlyCollection<TEntity>>(key);

        if (cached is not null)
            return cached;

        var result = await dataContext.SelectAsync(query);

        await cache.SetAsync(key, result);

        return result;
    }

    private async Task InvalidateCacheAsync()
    {
        if (cache is null)
            return;

        await cache.RemoveByPrefixAsync(GetCachePrefix());
    }

    private static string GetCachePrefix()
    {
        return $"{typeof(TEntity).FullName}:";
    }

    private static string BuildCacheKey(
        string operation,
        Query<TEntity>? query)
    {
        var queryJson = JsonSerializer.Serialize(query);

        return $"{GetCachePrefix()}{operation}:{queryJson}";
    }
}