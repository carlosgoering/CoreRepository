using DataAccess.Cache.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccess.Cache.MemoryCache.Configuration;

public static class Registration
{
    public static IServiceCollection AddDataAccessMemoryCache(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

        return services;
    }
}