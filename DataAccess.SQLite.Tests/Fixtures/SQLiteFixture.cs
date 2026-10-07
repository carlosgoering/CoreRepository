using DataAccess.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using SQLite;
using Xunit;

namespace DataAccess.SQLite.Tests.Fixtures;

public sealed class SQLiteFixture : IAsyncLifetime
{
    private SQLiteAsyncConnection? database;

    public SQLiteAsyncConnection Database =>
        database ?? throw new InvalidOperationException(
            "SQLite database has not been initialized.");

    public async Task InitializeAsync()
    {
        database = new SQLiteAsyncConnection(
            ":memory:",
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache);

        await database.ExecuteAsync("PRAGMA foreign_keys = ON;");
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
            await database.CloseAsync();
    }

    public IDataAccessContext<TestEntity> CreateContext()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        return new DataAccessContextTests<TestEntity>(
            Database,
            loggerFactory.CreateLogger<DataAccessContextTests<TestEntity>>());
    }
}