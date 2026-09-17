using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using DataAcess.Firestore.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataAcess.Firestore.Configuration
{
    public static class Registration
    {
        public static IServiceCollection AddFirestore(
           this IServiceCollection services,
           Action<Database> configure)
        {
            services.Configure(configure);

            services.AddFirestoreDb((serviceProvider, builder) =>
            {
                var database = serviceProvider
                    .GetRequiredService<IOptions<Database>>()
                    .Value;

                builder.ProjectId = database.ProjectId;
                builder.DatabaseId = database.DatabaseName;
            });

            services.AddSingleton(typeof(IDataAccessContext<>), typeof(DataAccessContext<>));
            services.AddScoped(typeof(IRepository<>), typeof(DataRepository<>));
            return services;
        }
    }
}
