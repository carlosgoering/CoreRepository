using SQLite;
using Xunit;

namespace DataAccess.SQLite.Tests.Fixtures;

public sealed class SQLiteFixture : IAsyncLifetime
{
    public SQLiteAsyncConnection database { get; private set; } = null!;

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
    
    public async Task CleanAsync()
    {
        await database.DeleteAllAsync<TestEntity>();
    }
}