using DataAccess.Abstractions.Attributes;
using DataAccess.Core.Metadata;
using MongoDB.Bson.Serialization;
using System.Reflection;

namespace DataAccess.MongoDB.ClassMaps;
internal static class ClassMapRegistration
{
    private static readonly HashSet<Type> registeredTypes = [];

    public static void Register<TEntity>()
        where TEntity : class, new()
    {
        var type = typeof(TEntity);

        if (registeredTypes.Contains(type))
            return;

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        BsonClassMap.RegisterClassMap<TEntity>(cm =>
        {
            cm.AutoMap();

            var member = cm.GetMemberMap(primaryKey.Name);

            cm.SetIdMember(member);
        });

        registeredTypes.Add(type);
    }
}