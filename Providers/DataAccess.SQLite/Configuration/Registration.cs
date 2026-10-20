using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.SQLite.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SQLite;

namespace DataAccess.SQLite.Configuration
{
    public static class Registration
    {
        public static IServiceCollection AddSqlite(
            this IServiceCollection services,
            Action<Database> configure)
        {
            services.Configure(configure);

            services.AddSingleton(sp =>
            {
                var options = sp
                    .GetRequiredService<IOptions<Database>>()
                    .Value;

                return new SQLiteAsyncConnection(
                    new SQLiteConnectionString(
                        options.ConnectionString,
                        true,
                        options.ConnectionKey));
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
}
