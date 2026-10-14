using DataAccess.PostgreSQL.ClassMaps;
using DataAccess.PostgreSQL.Tests.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DataAccess.PostgreSQL.Tests.Fixtures
{
    public class PostgreSqlFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer container =
            new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithDatabase("dataaccess_test")
                .WithUsername("postgres_test")
                .WithPassword("postgres_test")
                .Build();


        public NpgsqlDataSource DataSource { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await container.StartAsync();

            DataSource = NpgsqlDataSource.Create(
                container.GetConnectionString());

            await ClassMapRegistration.Register<TestEntity>(
                DataSource,
                NullLogger.Instance);
        }

        public async Task DisposeAsync()
        {
            await DataSource.DisposeAsync();
            await container.DisposeAsync();
        }
    }
}