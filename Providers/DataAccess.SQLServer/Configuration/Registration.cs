using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.SqlServer.Context;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataAccess.SqlServer.Configuration;

public static class Registration
{
    public static IServiceCollection AddSqlServer(
        this IServiceCollection services,
        Action<Database> configure)
    {
        services.Configure(configure);

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