using DataAccess.Cache.Abstractions;
using DataAccess.Cache.MemoryCache.Configuration;
using DataAccess.Cache.Redis.Configuration;
using DataAccess.Cache.Garnet.Configuration;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataAccess.Cache.Tests;

public sealed class CacheFixture : IAsyncLifetime
{
    private readonly IContainer redis = new ContainerBuilder("redis:7.4")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    private readonly IContainer garnet = new ContainerBuilder("ghcr.io/microsoft/garnet:1.0.99")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    public async Task InitializeAsync() { await Task.WhenAll(redis.StartAsync(), garnet.StartAsync()); }
    public async Task DisposeAsync() { await redis.DisposeAsync(); await garnet.DisposeAsync(); }
    public ServiceProvider Create(string backend)
    {
        var services = new ServiceCollection();
        if (backend == "Memory") services.AddDataAccessMemoryCache();
        else
        {
            var container = backend == "Redis" ? redis : garnet;
            var endpoint = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
            if (backend == "Redis") services.AddDataAccessRedisCache(endpoint);
            else services.AddDataAccessGarnetCache(endpoint);
        }
        return services.BuildServiceProvider();
    }
}

public sealed class CacheProviderTests(CacheFixture fixture) : IClassFixture<CacheFixture>
{
    [Theory]
    [InlineData("Memory")]
    [InlineData("Redis")]
    [InlineData("Garnet")]
    public async Task Contract(string backend)
    {
        using var services = fixture.Create(backend);
        var cache = services.GetRequiredService<ICacheProvider>();
        var prefix = Guid.NewGuid() + ":";
        Assert.Null(await cache.GetAsync<Item>(prefix + "missing"));
        Assert.Null(await cache.GetAsync<long?>(prefix + "missing"));
        await cache.SetAsync(prefix + "object", new Item("ação", [1, 2]));
        var item = await cache.GetAsync<Item>(prefix + "object");
        Assert.Equal("ação", item!.Name);
        Assert.Equal(new[] { 1, 2 }, item.Values);
        await cache.SetAsync(prefix + "zero", 0L);
        await cache.SetAsync(prefix + "false", false);
        Assert.Equal(0L, await cache.GetAsync<long?>(prefix + "zero"));
        Assert.Equal(false, await cache.GetAsync<bool?>(prefix + "false"));
        await cache.SetAsync(prefix + "object", new Item("updated", []));
        await Task.Delay(100); // Allow old memory eviction callbacks to finish.
        await cache.RemoveByPrefixAsync(prefix);
        Assert.Null(await cache.GetAsync<Item>(prefix + "object"));
        Assert.Null(await cache.GetAsync<long?>(prefix + "zero"));
        await cache.SetAsync(prefix + "ttl", "value", TimeSpan.FromMilliseconds(150));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (await cache.GetAsync<string>(prefix + "ttl") is not null && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        Assert.Null(await cache.GetAsync<string>(prefix + "ttl"));
        await cache.SetAsync(prefix + "remove", "value");
        await cache.RemoveAsync(prefix + "remove");
        await cache.RemoveAsync(prefix + "remove");
        Assert.Null(await cache.GetAsync<string>(prefix + "remove"));
    }

    [Theory]
    [InlineData("Memory")]
    [InlineData("Redis")]
    [InlineData("Garnet")]
    public async Task Prefix_Is_Literal_And_Does_Not_Remove_Other_Keys(string backend)
    {
        using var services = fixture.Create(backend);
        var cache = services.GetRequiredService<ICacheProvider>();
        var root = Guid.NewGuid() + ":";
        var prefix = root + "[*?]\\";
        await cache.SetAsync(prefix + "one", "match");
        await cache.SetAsync(root + "other", "keep");
        await cache.RemoveByPrefixAsync(prefix);
        Assert.Null(await cache.GetAsync<string>(prefix + "one"));
        Assert.Equal("keep", await cache.GetAsync<string>(root + "other"));
        await cache.RemoveByPrefixAsync(root);
    }
    public sealed record Item(string Name, int[] Values);
}
