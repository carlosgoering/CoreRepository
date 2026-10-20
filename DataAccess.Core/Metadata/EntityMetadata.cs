using DataAccess.Abstractions.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
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

    public static PropertyInfo? GetUnique<TEntity>()
        where TEntity : class
    {
        return typeof(TEntity)
            .GetProperties()
            .SingleOrDefault(x =>
                x.GetCustomAttribute<UniqueAttribute>() != null);
    }

    public static object? GetUniqueValue<TEntity>(TEntity entity)
        where TEntity : class
    {
        return GetUnique<TEntity>()?.GetValue(entity);
    }

    public static PropertyInfo? GetIndexed<TEntity>()
        where TEntity : class
    {
        return typeof(TEntity)
            .GetProperties()
            .SingleOrDefault(x =>
                x.GetCustomAttribute<IndexedAttribute>() != null);
    }

    public static object? GetIndexedValue<TEntity>(TEntity entity)
        where TEntity : class
    {
        return GetIndexed<TEntity>()?.GetValue(entity);
    }

    public static string GetTableName<TEntity>()
        where TEntity : class
    {
        var type = typeof(TEntity);

        var tableAttribute = type.GetCustomAttribute<TableAttribute>();

        return tableAttribute?.Name ?? type.Name;
    }
}