using DataAccess.Cache.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DataAccess.Cache.Redis.Configuration;

public static class Registration
{
    public static IServiceCollection AddDataAccessRedisCache(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        return services;
    }
}
