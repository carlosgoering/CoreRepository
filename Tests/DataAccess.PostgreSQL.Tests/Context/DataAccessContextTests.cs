using DataAccess.Abstractions.Models;
using DataAccess.PostgreSQL.Context;
using DataAccess.PostgreSQL.Tests.Entities;
using DataAccess.PostgreSQL.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataAccess.PostgreSQL.Tests.Context
{
    public sealed class DataAccessContextTests
    : IClassFixture<PostgreSqlFixture>
    {
        private readonly PostgreSqlFixture fixture;

        public DataAccessContextTests(PostgreSqlFixture fixture)
        {
            this.fixture = fixture;
        }

        private async Task<DataAccessContext<TestEntity>> CreateContextAsync()
        {
            var context = new DataAccessContext<TestEntity>(
                fixture.DataSource,
                NullLogger<DataAccessContext<TestEntity>>.Instance);

            await context.CountAsync();

            return context;
        }

        [Fact]
        public async Task Should_Insert_Entity()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active
            };

            await context.InsertAsync(entity);

            var result = await context.FirstOrDefaultAsync(
                new Query<TestEntity>
                {
                    Filters =
                    [
                        new QueryFilter(
                        nameof(TestEntity.Id),
                        QueryOperator.Equal,
                        entity.Id)
                    ]
                });

            Assert.NotNull(result);
            Assert.Equal(entity.Id, result.Id);
            Assert.Equal(entity.Name, result.Name);
            Assert.Equal(entity.Status, result.Status);
        }

        [Fact]
        public async Task Should_Select_Entity()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active
            };

            await context.InsertAsync(entity);

            var result = await context.SelectAsync(
                new Query<TestEntity>());

            Assert.Single(result);

            var selected = result.First();

            Assert.Equal(entity.Id, selected.Id);
            Assert.Equal(entity.Name, selected.Name);
            Assert.Equal(entity.Status, selected.Status);
        }

        [Fact]
        public async Task Should_Update_Entity()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Pending
            };

            await context.InsertAsync(entity);

            entity.Name = "John Updated";
            entity.Status = TestStatus.Active;

            await context.UpdateAsync(entity);

            var result = await context.FirstOrDefaultAsync(
                new Query<TestEntity>
                {
                    Filters =
                    [
                        new QueryFilter(
                        nameof(TestEntity.Id),
                        QueryOperator.Equal,
                        entity.Id)
                    ]
                });

            Assert.NotNull(result);
            Assert.Equal("John Updated", result.Name);
            Assert.Equal(TestStatus.Active, result.Status);
        }

        [Fact]
        public async Task Should_Delete_Entity()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active
            };

            await context.InsertAsync(entity);
            await context.DeleteAsync(entity);

            var exists = await context.ExistsAsync(
                new Query<TestEntity>
                {
                    Filters =
                    [
                        new QueryFilter(
                        nameof(TestEntity.Id),
                        QueryOperator.Equal,
                        entity.Id)
                    ]
                });

            Assert.False(exists);
        }

        [Fact]
        public async Task Should_Filter_Entity()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            await context.InsertAsync(new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active
            });

            await context.InsertAsync(new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Mary",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Pending
            });

            var result = await context.SelectAsync(
                new Query<TestEntity>
                {
                    Filters =
                    [
                        new QueryFilter(
                        nameof(TestEntity.Status),
                        QueryOperator.Equal,
                        TestStatus.Active)
                    ]
                });

            Assert.Single(result);
            Assert.Equal("John", result.First().Name);
        }

        [Fact]
        public async Task Should_Return_Paged_Result()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entities = Enumerable
                .Range(1, 5)
                .Select(i => new TestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"User {i}",
                    CreatedAt = DateTime.UtcNow,
                    Status = TestStatus.Active
                })
                .ToArray();

            foreach (var entity in entities)
                await context.InsertAsync(entity);

            var result = await context.SelectPagedAsync(
                new Query<TestEntity>
                {
                    Order = new QueryOrder(
                        nameof(TestEntity.Name)),

                    Page = 2,
                    PageSize = 2
                });

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.PageSize);

            Assert.Equal("User 3", result.Items.First().Name);
            Assert.Equal("User 4", result.Items.Last().Name);
        }

        [Fact]
        public async Task Should_Return_True_When_Entity_Exists()
        {
            var context = await CreateContextAsync();

            await ClearTableAsync();

            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "John",
                CreatedAt = DateTime.UtcNow,
                Status = TestStatus.Active
            };

            await context.InsertAsync(entity);

            var exists = await context.ExistsAsync(
                new Query<TestEntity>
                {
                    Filters =
                    [
                        new QueryFilter(
                        nameof(TestEntity.Id),
                        QueryOperator.Equal,
                        entity.Id)
                    ]
                });

            Assert.True(exists);
        }

        private async Task ClearTableAsync()
        {
            await using var connection =
                await fixture.DataSource.OpenConnectionAsync();

            await using var command = new Npgsql.NpgsqlCommand(
                """DELETE FROM "TestEntity";""",
                connection);

            await command.ExecuteNonQueryAsync();
        }
    }
}