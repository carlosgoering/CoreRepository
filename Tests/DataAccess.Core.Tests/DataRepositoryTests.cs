using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Repository;
using Xunit;

namespace DataAccess.Core.Tests;

public sealed class DataRepositoryTests
{
    [Fact]
    public async Task Writes_Use_The_Same_Entity_And_Return_It_After_Completion()
    {
        var context = new RecordingContext();
        var repository = new DataRepository<Entity>(context);
        var entity = new Entity();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.WriteTask = completion.Task;
        var insert = repository.InsertAsync(entity);
        Assert.False(insert.IsCompleted);
        Assert.Same(entity, context.Entity);
        Assert.Equal("Insert", context.Operation);
        completion.SetResult();
        Assert.Same(entity, await insert);
        Assert.Same(entity, await repository.UpdateAsync(entity));
        Assert.Equal("Update", context.Operation);
        await repository.DeleteAsync(entity);
        Assert.Same(entity, context.Entity);
        Assert.Equal("Delete", context.Operation);
    }

    [Fact]
    public async Task Reads_Pass_The_Original_Query_And_Return_Context_Results()
    {
        var context = new RecordingContext();
        var repository = new DataRepository<Entity>(context);
        var query = new Query<Entity> { Page = 3, PageSize = 7 };
        Assert.Same(context.Result, await repository.FirstOrDefaultAsync(query));
        Assert.Same(query, context.Query);
        Assert.Same(context.Items, await repository.SelectAsync(query));
        Assert.Same(query, context.Query);
        Assert.Same(context.Page, await repository.SelectPagedAsync(query));
        Assert.Same(query, context.Query);
        Assert.Equal(12L, await repository.CountAsync(query));
        Assert.Same(query, context.Query);
        Assert.True(await repository.ExistsAsync(query));
        Assert.Same(query, context.Query);
        await repository.CountAsync();
        Assert.Null(context.Query);
        context.Result = null;
        Assert.Null(await repository.FirstOrDefaultAsync(query));
    }

    [Fact]
    public async Task Write_Failures_Propagate_To_The_Caller()
    {
        var error = new InvalidOperationException("Database unavailable");
        var context = new RecordingContext { WriteTask = Task.FromException(error) };
        var repository = new DataRepository<Entity>(context);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InsertAsync(new())));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(new())));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(new())));
    }

    public sealed class Entity { }
    private sealed class RecordingContext : IDataAccessContext<Entity>
    {
        public Entity? Entity { get; private set; }
        public Query<Entity>? Query { get; private set; }
        public string? Operation { get; private set; }
        public Task WriteTask { get; set; } = Task.CompletedTask;
        public Entity? Result { get; set; } = new();
        public IReadOnlyCollection<Entity> Items { get; } = new[] { new Entity() };
        public PagedResult<Entity> Page { get; } = new() { Items = [], Page = 3, PageSize = 7, TotalCount = 12 };
        private Task Write(Entity entity, string operation)
        {
            Entity = entity;
            Operation = operation;
            return WriteTask;
        }
        public Task InsertAsync(Entity entity) => Write(entity, "Insert");
        public Task UpdateAsync(Entity entity) => Write(entity, "Update");
        public Task DeleteAsync(Entity entity) => Write(entity, "Delete");
        public Task<Entity?> FirstOrDefaultAsync(Query<Entity> query) { Query = query; return Task.FromResult(Result); }
        public Task<IReadOnlyCollection<Entity>> SelectAsync(Query<Entity> query) { Query = query; return Task.FromResult(Items); }
        public Task<PagedResult<Entity>> SelectPagedAsync(Query<Entity> query) { Query = query; return Task.FromResult(Page); }
        public Task<long> CountAsync(Query<Entity>? query = null) { Query = query; return Task.FromResult(12L); }
        public Task<bool> ExistsAsync(Query<Entity> query) { Query = query; return Task.FromResult(true); }
    }
}
