using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.PostgreSQL.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DataAccess.PostgreSQL.Configuration;

public static class Registration
{
    public static IServiceCollection AddPostgreSQL(
        this IServiceCollection services,
        Action<Database> configure)
    {
        services.Configure(configure);

        services.AddSingleton(sp =>
        {
            var database = sp
                .GetRequiredService<IOptions<Database>>()
                .Value;

            return NpgsqlDataSource.Create(
                database.ConnectionString);
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