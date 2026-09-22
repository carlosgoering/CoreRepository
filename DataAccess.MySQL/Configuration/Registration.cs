using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.MySQL.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;

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

            var dataSourceBuilder =
                new MySqlDataSourceBuilder(database.ConnectionString);
            
            return dataSourceBuilder.Build();
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