using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace DataAccess.MongoDB.Tests.Fixtures;

public sealed class MongoDBFixture : IAsyncLifetime
{
   private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:8")
        .Build();
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        var client = new MongoClient(_mongoContainer.GetConnectionString());

        Database = client.GetDatabase("dataaccess_test");
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    public async Task ClearAsync()
    {
        await Database
            .GetCollection<TestEntity>(nameof(TestEntity))
            .DeleteManyAsync(Builders<TestEntity>.Filter.Empty);
    }
}