using DataAcess.Firestore.Context;
using DataAccess.Firestore.Tests.Entities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Google.Cloud.Firestore;
using Grpc.Core;
using Xunit;

namespace DataAccess.Firestore.Tests.Fixtures;

public sealed class FirestoreFixture : IAsyncLifetime
{
    private readonly IContainer container = new ContainerBuilder("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
        .WithCommand("gcloud", "beta", "emulators", "firestore", "start", "--host-port=0.0.0.0:8080", "--project=demo-dataaccess", "--quiet")
        .WithPortBinding(8080, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8080))
        .Build();

    private FirestoreDb database = null!;
    internal DataAccessContext<TestEntity> Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        database = new FirestoreDbBuilder
        {
            ProjectId = "demo-dataaccess",
            Endpoint = $"{container.Hostname}:{container.GetMappedPublicPort(8080)}",
            ChannelCredentials = ChannelCredentials.Insecure
        }.Build();
        Context = new(database);
    }

    public async Task ClearAsync()
    {
        var snapshot = await database.Collection(nameof(TestEntity)).GetSnapshotAsync();
        foreach (var document in snapshot.Documents)
            await document.Reference.DeleteAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
