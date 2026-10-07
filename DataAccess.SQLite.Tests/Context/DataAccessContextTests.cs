using DataAccess.Abstractions.Models;
using DataAccess.SQLite.Tests.Fixtures;
using Xunit;

namespace DataAccess.SQLite.Tests;

public sealed class DataAccessContextTests : IClassFixture<SQLiteFixture>
{
    private readonly SQLiteFixture fixture;

    public DataAccessContextTests(SQLiteFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InsertAsync_ShouldInsertEntity()
    {
        var context = fixture.CreateContext();

        var entity = CreateEntity("1");

        await context.InsertAsync(entity);

        var result = await context.FirstOrDefaultAsync(
            new Query<TestEntity>
            {
                Filters =
                [
                    new QueryFilter
                    {
                        Field = nameof(TestEntity.Id),
                        Operator = QueryOperator.Equal,
                        Value = "1"
                    }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal("1", result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateEntity()
    {
        var context = fixture.CreateContext();

        var entity = CreateEntity("2");

        await context.InsertAsync(entity);

        entity.Name = "Updated";

        await context.UpdateAsync(entity);

        var result = await context.FirstOrDefaultAsync(
            QueryById("2"));

        Assert.NotNull(result);
        Assert.Equal("Updated", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteEntity()
    {
        var context = fixture.CreateContext();

        var entity = CreateEntity("3");

        await context.InsertAsync(entity);
        await context.DeleteAsync(entity);

        var result = await context.FirstOrDefaultAsync(
            QueryById("3"));

        Assert.Null(result);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnMatchingEntity()
    {
        var context = fixture.CreateContext();

        await context.InsertAsync(CreateEntity("4"));

        var result = await context.FirstOrDefaultAsync(
            new Query<TestEntity>
            {
                Filters =
                [
                    new QueryFilter
                    {
                        Field = nameof(TestEntity.Email),
                        Operator = QueryOperator.Equal,
                        Value = "test4@test.com"
                    }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal("4", result.Id);
    }

    [Fact]
    public async Task SelectAsync_ShouldApplyFilters()
    {
        var context = fixture.CreateContext();

        await context.InsertAsync(CreateEntity("5", "A"));
        await context.InsertAsync(CreateEntity("6", "A"));
        await context.InsertAsync(CreateEntity("7", "B"));

        var result = await context.SelectAsync(
            new Query<TestEntity>
            {
                Filters =
                [
                    new QueryFilter
                    {
                        Field = nameof(TestEntity.Category),
                        Operator = QueryOperator.Equal,
                        Value = "A"
                    }
                ],
                PageSize = 10
            });

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.Equal("A", x.Category));
    }

    [Fact]
    public async Task SelectAsync_ShouldSupportIn()
    {
        var context = fixture.CreateContext();

        await context.InsertAsync(CreateEntity("8"));
        await context.InsertAsync(CreateEntity("9"));
        await context.InsertAsync(CreateEntity("10"));

        var result = await context.SelectAsync(
            new Query<TestEntity>
            {
                Filters =
                [
                    new QueryFilter
                    {
                        Field = nameof(TestEntity.Id),
                        Operator = QueryOperator.In,
                        Value = new[] { "8", "10" }
                    }
                ],
                PageSize = 10
            });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Id == "8");
        Assert.Contains(result, x => x.Id == "10");
    }

    [Fact]
    public async Task SelectPagedAsync_ShouldReturnCorrectPage()
    {
        var context = fixture.CreateContext();

        for (var i = 1; i <= 5; i++)
            await context.InsertAsync(CreateEntity($"page-{i}"));

        var result = await context.SelectPagedAsync(
            new Query<TestEntity>
            {
                Page = 2,
                PageSize = 2,
                Order = new QueryOrder
                {
                    Field = nameof(TestEntity.Id),
                    Descending = false
                }
            });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        var context = fixture.CreateContext();

        await context.InsertAsync(CreateEntity("count-1"));
        await context.InsertAsync(CreateEntity("count-2"));

        var count = await context.CountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueWhenEntityExists()
    {
        var context = fixture.CreateContext();

        await context.InsertAsync(CreateEntity("exists-1"));

        var result = await context.ExistsAsync(
            QueryById("exists-1"));

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseWhenEntityDoesNotExist()
    {
        var context = fixture.CreateContext();

        var result = await context.ExistsAsync(
            QueryById("does-not-exist"));

        Assert.False(result);
    }

    [Fact]
    public async Task NullableDateTime_ShouldPreserveNull()
    {
        var context = fixture.CreateContext();

        var entity = CreateEntity("nullable-1");
        entity.UpdatedAt = null;

        await context.InsertAsync(entity);

        var result = await context.FirstOrDefaultAsync(
            QueryById("nullable-1"));

        Assert.NotNull(result);
        Assert.Null(result.UpdatedAt);
    }

    [Fact]
    public async Task Enum_ShouldBePersistedAndReadCorrectly()
    {
        var context = fixture.CreateContext();

        var entity = CreateEntity("enum-1");
        entity.Status = TestStatus.Active;

        await context.InsertAsync(entity);

        var result = await context.FirstOrDefaultAsync(
            QueryById("enum-1"));

        Assert.NotNull(result);
        Assert.Equal(TestStatus.Active, result.Status);
    }

    private static TestEntity CreateEntity(
        string id,
        string category = "Default")
    {
        return new TestEntity
        {
            Id = id,
            Name = "Test",
            Email = $"test{id}@test.com",
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = TestStatus.Active
        };
    }

    private static Query<TestEntity> QueryById(string id)
    {
        return new Query<TestEntity>
        {
            Filters =
            [
                new QueryFilter
                {
                    Field = nameof(TestEntity.Id),
                    Operator = QueryOperator.Equal,
                    Value = id
                }
            ]
        };
    }
}