using DataAccess.Cache.Redis;
using StackExchange.Redis;

namespace DataAccess.Cache.Garnet;

public sealed class GarnetCacheProvider(IConnectionMultiplexer connection) : RedisCacheProvider(connection, 0);
