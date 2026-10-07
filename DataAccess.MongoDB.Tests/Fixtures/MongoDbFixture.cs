using DataAccess.Abstractions.Interfaces;
using MongoDB.Driver;
using Xunit;

namespace DataAccess.MongoDB.Tests.Fixtures;

public sealed class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer container =
        new MongoDbBuilder()
            .WithImage("mongo:8")
            .Build();

    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        var client = new MongoClient(container.GetConnectionString());

        Database = client.GetDatabase("dataaccess_test");
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public DataAccessContext<TestEntity> CreateContext()
    {
        return new DataAccessContext<TestEntity>(Database);
    }

    public async Task ClearAsync()
    {
        await Database
            .GetCollection<TestEntity>(nameof(TestEntity))
            .DeleteManyAsync(Builders<TestEntity>.Filter.Empty);
    }
}