using DataAccess.Cache.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DataAccess.Cache.Garnet.Configuration;

public static class Registration
{
    public static IServiceCollection AddDataAccessGarnetCache(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ICacheProvider, GarnetCacheProvider>();
        return services;
    }
}
