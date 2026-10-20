using System.Text.Json;
using DataAccess.Cache.Abstractions;
using StackExchange.Redis;

namespace DataAccess.Cache.Redis;

public class RedisCacheProvider : ICacheProvider
{
    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;

    public RedisCacheProvider(IConnectionMultiplexer connection, int database = -1)
    {
        ArgumentNullException.ThrowIfNull(connection);
        this.connection = connection;
        this.database = connection.GetDatabase(database);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var value = await database.StringGetAsync(key);
        return value.IsNull ? default : JsonSerializer.Deserialize<T>((string)value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration));
        await database.StringSetAsync(key, JsonSerializer.Serialize(value), expiration);
    }

    public async Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await database.KeyDeleteAsync(key);
    }

    // Prefix invalidation is a scan, not an atomic operation with concurrent writes.
    public async Task RemoveByPrefixAsync(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        var pattern = string.Concat(prefix.Select(c => "*?[]\\".Contains(c) ? "\\" + c : c.ToString())) + "*";
        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica) continue;
            await foreach (var key in server.KeysAsync(database.Database, pattern, pageSize: 250))
                await database.KeyDeleteAsync(key);
        }
    }
}
