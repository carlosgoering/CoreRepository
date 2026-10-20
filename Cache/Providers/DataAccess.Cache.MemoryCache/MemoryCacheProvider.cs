using DataAccess.Cache.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace DataAccess.Cache.MemoryCache;

public sealed class MemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache cache;
    private readonly ConcurrentDictionary<string, object> keys = new();

    public MemoryCacheProvider(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        cache.TryGetValue(key, out T? value);

        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiration;

        var token = new object();
        options.RegisterPostEvictionCallback(
            (_, _, _, _) =>
            {
                ((ICollection<KeyValuePair<string, object>>)keys).Remove(new(key, token));
            });

        keys[key] = token;
        cache.Set(key, value, options);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);
        keys.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        foreach (var key in keys.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            cache.Remove(key);
            keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}