using DataAccess.Abstractions.Attributes;
using DataAccess.PostgreSQL.ClassMaps;
using DataAccess.PostgreSQL.Tests.Entities;
using DataAccess.PostgreSQL.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace DataAccess.PostgreSQL.Tests.Schema
{
    public class ClassMapRegistrationTests : IClassFixture<PostgreSqlFixture>
    {
        private readonly PostgreSqlFixture fixture;

        public ClassMapRegistrationTests(PostgreSqlFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task Should_Create_Table()
        {
            await RegisterTableAsync();

            await using var connection =
                await fixture.DataSource.OpenConnectionAsync();

            await using var command = new NpgsqlCommand(
                """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_name = 'TestEntity'
            );
            """,
                connection);

            var exists = (bool)await command.ExecuteScalarAsync()!;

            Assert.True(exists);
        }

        [Fact]
        public async Task Should_Create_Primary_Key()
        {
            await RegisterTableAsync();

            await using var connection =
                await fixture.DataSource.OpenConnectionAsync();

            await using var command = new NpgsqlCommand(
                """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name
                WHERE tc.table_name = 'TestEntity'
                  AND tc.constraint_type = 'PRIMARY KEY'
                  AND ccu.column_name = 'Id'
            );
            """,
                connection);

            var exists = (bool)await command.ExecuteScalarAsync()!;

            Assert.True(exists);
        }

        [Fact]
        public async Task Should_Create_NonNullable_Column()
        {
            await RegisterTableAsync();

            var nullable = await GetColumnNullableAsync("Name");

            Assert.Equal("NO", nullable);
        }

        [Fact]
        public async Task Should_Create_Nullable_DateTime_Column()
        {
            await RegisterTableAsync();

            var nullable = await GetColumnNullableAsync("UpdatedAt");

            Assert.Equal("YES", nullable);
        }

        [Fact]
        public async Task Should_Create_DateTime_As_TimestampWithTimeZone()
        {
            await RegisterTableAsync();

            var dataType = await GetColumnDataTypeAsync("CreatedAt");

            Assert.Equal(
                "timestamp with time zone",
                dataType);
        }

        [Fact]
        public async Task Should_Create_Enum_As_SmallInt()
        {
            await RegisterTableAsync();

            var dataType = await GetColumnDataTypeAsync("Status");

            Assert.Equal(
                "smallint",
                dataType);
        }

        private async Task RegisterTableAsync()
        {
            await ClassMapRegistration.Register<TestEntity>(
                fixture.DataSource,
                NullLogger.Instance);
        }

        private async Task<string> GetColumnNullableAsync(
            string columnName)
        {
            await using var connection =
                await fixture.DataSource.OpenConnectionAsync();

            await using var command = new NpgsqlCommand(
                """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_name = 'TestEntity'
              AND column_name = @column;
            """,
                connection);

            command.Parameters.AddWithValue(
                "column",
                columnName);

            return (string)(await command.ExecuteScalarAsync())!;
        }

        private async Task<string> GetColumnDataTypeAsync(
            string columnName)
        {
            await using var connection =
                await fixture.DataSource.OpenConnectionAsync();

            await using var command = new NpgsqlCommand(
                """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_name = 'TestEntity'
              AND column_name = @column;
            """,
                connection);

            command.Parameters.AddWithValue(
                "column",
                columnName);

            return (string)(await command.ExecuteScalarAsync())!;
        }
    }
}