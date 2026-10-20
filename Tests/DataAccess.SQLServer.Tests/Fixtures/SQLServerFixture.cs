using DataAccess.SqlServer.Context;
using DataAccess.SQLServer.Tests.Entities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataAccess.SQLServer.Tests.Fixtures;

public sealed class SQLServerFixture : IAsyncLifetime
{
    private readonly IContainer container = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("MSSQL_SA_PASSWORD", "DataAccess_Test123!")
        .WithPortBinding(1433, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .Build();

    internal DataAccessContext<TestEntity> Context { get; private set; } = null!;
    private string ConnectionString => new SqlConnectionStringBuilder
    {
        DataSource = $"{container.Hostname},{container.GetMappedPublicPort(1433)}",
        UserID = "sa", Password = "DataAccess_Test123!", TrustServerCertificate = true
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        Context = new(ConnectionString, NullLogger<DataAccessContext<TestEntity>>.Instance);
        await Context.CountAsync();
    }

    public async Task ClearAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM [TestEntity]", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
