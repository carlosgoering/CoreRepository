namespace DataAccess.Cache.Abstractions;

public interface ICacheProvider
{
    Task<T?> GetAsync<T>(string key);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null);

    Task RemoveAsync(string key);

    Task RemoveByPrefixAsync(string prefix);
}
