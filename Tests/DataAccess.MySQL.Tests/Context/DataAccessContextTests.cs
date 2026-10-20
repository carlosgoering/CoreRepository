using DataAccess.Abstractions.Models;
using DataAccess.MySQL.Tests.Entities;
using DataAccess.MySQL.Tests.Fixtures;
using Xunit;
namespace DataAccess.MySQL.Tests.Context;
public sealed class DataAccessContextTests(MySQLFixture fixture) : IClassFixture<MySQLFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ClearAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_RoundTrips_All_Properties()
    {
        var entity = new TestEntity { Name = "O'Brien", Age = 30, OptionalAge = 42 };
        await fixture.Context.InsertAsync(entity);
        var result = await fixture.Context.FirstOrDefaultAsync(ById(entity));
        Assert.NotNull(result);
        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.Name, result.Name);
        Assert.Equal(entity.Age, result.Age);
        Assert.Equal(entity.OptionalAge, result.OptionalAge);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public async Task Update_And_Delete_Target_Only_The_Primary_Key()
    {
        var entity = new TestEntity { Name = "Original", OptionalAge = 10 };
        var other = new TestEntity { Name = "Other" };
        await fixture.Context.InsertAsync(entity);
        await fixture.Context.InsertAsync(other);
        entity.Name = "Updated";
        entity.OptionalAge = null;
        await fixture.Context.UpdateAsync(entity);
        var updated = await fixture.Context.FirstOrDefaultAsync(ById(entity));
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Null(updated.OptionalAge);
        Assert.True(await fixture.Context.ExistsAsync(ById(entity)));
        await fixture.Context.DeleteAsync(entity);
        Assert.False(await fixture.Context.ExistsAsync(ById(entity)));
        Assert.Null(await fixture.Context.FirstOrDefaultAsync(ById(entity)));
        Assert.Equal("Other", (await fixture.Context.FirstOrDefaultAsync(ById(other)))!.Name);
        Assert.Equal(1L, await fixture.Context.CountAsync());
    }

    [Theory]
    [InlineData(QueryOperator.Equal, 1)]
    [InlineData(QueryOperator.NotEqual, 2)]
    [InlineData(QueryOperator.GreaterThan, 1)]
    [InlineData(QueryOperator.GreaterThanOrEqual, 2)]
    [InlineData(QueryOperator.LessThan, 1)]
    [InlineData(QueryOperator.LessThanOrEqual, 2)]
    public async Task Filters_Use_The_Requested_Operator(QueryOperator operation, int expected)
    {
        await SeedAsync();
        var query = new Query<TestEntity> { Filters = [new(nameof(TestEntity.Age), operation, 20)] };
        var result = await fixture.Context.SelectAsync(query);
        Assert.Equal(expected, result.Count);
        Assert.Equal((long)expected, await fixture.Context.CountAsync(query));
    }

    [Fact]
    public async Task In_Filter_Selects_Only_Matching_Values()
    {
        await SeedAsync();
        var result = await fixture.Context.SelectAsync(new Query<TestEntity>
        {
            Filters = [new(nameof(TestEntity.Name), QueryOperator.In, new object[] { "User 1", "User 3" })],
            Order = new(nameof(TestEntity.Name))
        });
        Assert.Equal(new[] { "User 1", "User 3" }, result.Select(x => x.Name));
    }

    [Theory]
    [InlineData(false, "User 2", "User 3")]
    [InlineData(true, "User 2", "User 1")]
    public async Task Paging_Preserves_Total_And_Order(bool descending, string first, string second)
    {
        await SeedAsync();
        var result = await fixture.Context.SelectPagedAsync(new Query<TestEntity>
        {
            Order = new(nameof(TestEntity.Age), descending), Page = 2, PageSize = 1
        });
        Assert.Equal(3L, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(first, Assert.Single(result.Items).Name);
        var last = await fixture.Context.SelectPagedAsync(new Query<TestEntity>
        { Order = new(nameof(TestEntity.Age), descending), Page = 3, PageSize = 1 });
        Assert.Equal(second, Assert.Single(last.Items).Name);
    }

    [Fact]
    public async Task Empty_Queries_Return_Empty_Results()
    {
        Assert.Null(await fixture.Context.FirstOrDefaultAsync(new()));
        Assert.Empty(await fixture.Context.SelectAsync(new()));
        Assert.False(await fixture.Context.ExistsAsync(new()));
        var page = await fixture.Context.SelectPagedAsync(new());
        Assert.Empty(page.Items);
        Assert.Equal(0L, page.TotalCount);
        Assert.Equal(0L, await fixture.Context.CountAsync());
    }

    [Fact]
    public async Task Filtered_Paging_Counts_All_Matches_Beyond_The_Current_Page()
    {
        await SeedAsync();
        var query = new Query<TestEntity>
        {
            Filters = [new(nameof(TestEntity.Age), QueryOperator.GreaterThanOrEqual, 20)],
            Order = new(nameof(TestEntity.Age)), Page = 2, PageSize = 1
        };
        var page = await fixture.Context.SelectPagedAsync(query);
        Assert.Equal(2L, page.TotalCount);
        Assert.Equal("User 3", Assert.Single(page.Items).Name);
        var beyond = await fixture.Context.SelectPagedAsync(query with { Page = 3 });
        Assert.Empty(beyond.Items);
        Assert.Equal(2L, beyond.TotalCount);
    }

    [Fact]
    public async Task Select_Returns_All_Rows_While_First_Returns_Only_The_First()
    {
        await SeedAsync();
        var query = new Query<TestEntity> { Order = new(nameof(TestEntity.Age), true) };
        var results = await fixture.Context.SelectAsync(query);
        Assert.Equal(new[] { 30, 20, 10 }, results.Select(x => x.Age));
        Assert.Equal(30, (await fixture.Context.FirstOrDefaultAsync(query))!.Age);
    }

    [Fact]
    public async Task Empty_In_Filter_Returns_No_Matches()
    {
        await SeedAsync();
        var query = new Query<TestEntity>
        { Filters = [new(nameof(TestEntity.Id), QueryOperator.In, Array.Empty<Guid>())] };
        Assert.Empty(await fixture.Context.SelectAsync(query));
        Assert.Equal(0L, await fixture.Context.CountAsync(query));
        Assert.False(await fixture.Context.ExistsAsync(query));
    }

    [Fact]
    public async Task Multiple_Filters_Are_Combined_With_And()
    {
        await SeedAsync();
        var query = new Query<TestEntity>
        {
            Filters =
            [
                new(nameof(TestEntity.Age), QueryOperator.In, new[] { 10, 20, 30 }),
                new(nameof(TestEntity.Age), QueryOperator.GreaterThan, 10),
                new(nameof(TestEntity.Age), QueryOperator.LessThan, 30)
            ]
        };
        Assert.Equal(20, Assert.Single(await fixture.Context.SelectAsync(query)).Age);
        Assert.Equal(1L, await fixture.Context.CountAsync(query));
    }

    [Fact]
    public async Task Duplicate_Primary_Key_Is_Rejected()
    {
        var entity = new TestEntity { Name = "Original" };
        await fixture.Context.InsertAsync(entity);
        await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => fixture.Context.InsertAsync(entity));
        Assert.Equal(1L, await fixture.Context.CountAsync());
        Assert.Equal("Original", (await fixture.Context.FirstOrDefaultAsync(ById(entity)))!.Name);
    }

    private async Task SeedAsync()
    {
        for (var i = 1; i <= 3; i++)
            await fixture.Context.InsertAsync(new TestEntity { Name = $"User {i}", Age = i * 10 });
    }
    private static Query<TestEntity> ById(TestEntity entity) => new()
    { Filters = [new(nameof(TestEntity.Id), QueryOperator.Equal, entity.Id)] };
}
