using DataAccess.Abstractions.Attributes;
using System.Reflection;

namespace DataAccess.Core.Metadata;

public static class EntityMetadata
{
    public static PropertyInfo GetPrimaryKey<TEntity>()
        where TEntity : class
    {
        var primaryKey = typeof(TEntity)
            .GetProperties()
            .SingleOrDefault(x =>
                x.GetCustomAttribute<PrimaryKeyAttribute>() != null);

        if (primaryKey is null)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' must have a [PrimaryKey].");

        return primaryKey;
    }

    public static object? GetPrimaryKeyValue<TEntity>(TEntity entity)
        where TEntity : class
    {
        return GetPrimaryKey<TEntity>().GetValue(entity);
    }
}
