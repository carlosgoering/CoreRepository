using DataAccess.MySQL.Context;
using DataAccess.MySQL.Tests.Entities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

namespace DataAccess.MySQL.Tests.Fixtures;

public sealed class MySQLFixture : IAsyncLifetime
{
    private readonly IContainer container = new ContainerBuilder("mysql:8.4")
        .WithEnvironment("MYSQL_ROOT_PASSWORD", "DataAccess_Root123!")
        .WithEnvironment("MYSQL_DATABASE", "dataaccess_test")
        .WithEnvironment("MYSQL_USER", "dataaccess_test")
        .WithEnvironment("MYSQL_PASSWORD", "DataAccess_Test123!")
        .WithPortBinding(3306, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
            "mysql", "--protocol=TCP", "-h", "127.0.0.1", "-udataaccess_test",
            "-pDataAccess_Test123!", "dataaccess_test", "-e", "SELECT 1"))
        .Build();

    internal DataAccessContext<TestEntity> Context { get; private set; } = null!;
    private string ConnectionString => new MySqlConnectionStringBuilder
    {
        Server = container.Hostname,
        Port = container.GetMappedPublicPort(3306),
        Database = "dataaccess_test",
        UserID = "dataaccess_test",
        Password = "DataAccess_Test123!"
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        Context = new(ConnectionString, NullLogger<DataAccessContext<TestEntity>>.Instance);
        await Context.CountAsync();
    }

    public async Task ClearAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand("DELETE FROM `TestEntity`", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
