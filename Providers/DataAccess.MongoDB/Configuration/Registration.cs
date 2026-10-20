using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAccess.MongoDB.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DataAccess.MongoDB.Configuration
{
    public static class Registration
    {
        public static IServiceCollection AddMongo(
               this IServiceCollection services,
               Action<Database> configure)
        {
            services.Configure(configure);

            services.AddSingleton<IMongoClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Database>>().Value;

                return new MongoClient(options.ConnectionString);
            });

            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<Database>>().Value;

                return sp.GetRequiredService<IMongoClient>()
                         .GetDatabase(options.DatabaseName);
            });

            services.AddSingleton(typeof(IDataAccessContext<>), typeof(DataAccessContext<>));
            services.AddScoped(typeof(IRepository<>), typeof(DataRepository<>));

            return services;
        }
    }
}
