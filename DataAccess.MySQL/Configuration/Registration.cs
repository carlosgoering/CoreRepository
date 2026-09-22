using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.MySQL.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataAccess.MySQL.Configuration;

public static class Registration
{
    public static IServiceCollection AddMySQL(
        this IServiceCollection services,
        Action<Database> configure)
    {
        services.Configure(configure);

        var connectionString = string.Empty;

        services.AddSingleton(sp =>
        {
            var database = sp
                .GetRequiredService<IOptions<Database>>()
                .Value;

            return database.ConnectionString;
        });

        services.AddSingleton(
            typeof(IDataAccessContext<>),
            typeof(DataAccessContext<>));

        services.AddScoped(
            typeof(IRepository<>),
            typeof(DataRepository<>));

        return services;
    }
}