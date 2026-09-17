using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Metadata;
using DataAccess.SQLite.ClassMap;
using Microsoft.Extensions.Logging;
using SQLite;
using System.Collections;
using System.Linq.Expressions;
using System.Text;

namespace DataAccess.SQLite.Context;

/// <summary>
/// If you want to know more about SQLite, please visit: https://github.com/praeclarum/sqlite-net
/// </summary>
/// <typeparam name="TEntity"></typeparam>
internal sealed class DataAccessContext<TEntity> : IDataAccessContext<TEntity> where TEntity : class, new()
{
    private readonly SQLiteAsyncConnection database;
    private readonly ILogger<DataAccessContext<TEntity>> logger;
    private readonly Task initialization;
    public DataAccessContext(SQLiteAsyncConnection database, ILogger<DataAccessContext<TEntity>> logger)
    {
        this.logger = logger;

        this.database = database;

        initialization = ClassMapRegistration.Register<TEntity>(database, logger);
    }

    public async Task InsertAsync(TEntity entity)
    {
        await initialization;
        await database.InsertAsync(entity);
    }

    public async Task UpdateAsync(TEntity entity)
    {
        await initialization;

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var primaryKeyValue = EntityMetadata.GetPrimaryKeyValue(entity)?.ToString();

        if (primaryKeyValue is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has a null primary key.");
        }

        var properties = typeof(TEntity)
            .GetProperties()
            .Where(x =>
                x.CanRead &&
                x.CanWrite &&
                x != primaryKey)
            .ToArray();

        var setClause = string.Join(
            ", ",
            properties.Select(x => $"\"{x.Name}\" = ?"));

        var values = properties
            .Select(x => x.GetValue(entity))
            .Append(primaryKeyValue)
            .ToArray();

        var tableName = typeof(TEntity).Name;

        var sql = $"""
        UPDATE "{tableName}"
        SET {setClause}
        WHERE "{primaryKey.Name}" = ?
        """;

        logger.LogDebug(
            $"[SQLite] UPDATE '{tableName}' WHERE '{primaryKey.Name}' = '{primaryKeyValue}'");

        await database.ExecuteAsync(sql, values);
    }

    public async Task DeleteAsync(TEntity entity)
    {
        await initialization;

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var primaryKeyValue = EntityMetadata.GetPrimaryKeyValue(entity)?.ToString();

        if (primaryKeyValue is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has a null primary key.");
        }

        var tableName = typeof(TEntity).Name;

        var sql = $"""
        DELETE FROM "{tableName}"
        WHERE "{primaryKey.Name}" = ?
        """;

        logger.LogDebug(
            $"[SQLite] DELETE '{tableName}' WHERE '{primaryKey.Name}' = '{primaryKeyValue}'");

        await database.ExecuteAsync(
            sql,
            primaryKeyValue);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Query<TEntity> query)
    {
        await initialization;

        logger.LogDebug($"[SQLite] FirstOrDefault - Entity: {typeof(TEntity).Name}");

        var table = ApplyFilters(
            database.Table<TEntity>(),
            query);

        table = ApplyOrder(table, query);

        return await table.FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<TEntity>> SelectAsync(
        Query<TEntity> query)
    {
        await initialization;

        if (HasInFilter(query))
            return await ExecuteInQueryAsync(query);

        var table = ApplyFilters(
            database.Table<TEntity>(),
            query);

        table = ApplyOrder(table, query);

        return await table
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();
    }

    public async Task<PagedResult<TEntity>> SelectPagedAsync(
        Query<TEntity> query)
    {
        await initialization;

        var table = ApplyFilters(
            database.Table<TEntity>(),
            query);

        var total = await table.CountAsync();

        table = ApplyOrder(table, query);

        var items = await table
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<TEntity>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<long> CountAsync(
        Query<TEntity>? query = null)
    {
        await initialization;

        var table = database.Table<TEntity>();

        if (query is not null)
            table = ApplyFilters(table, query);

        return await table.CountAsync();
    }

    public async Task<bool> ExistsAsync(Query<TEntity> query)
    {
        await initialization;

        var table = ApplyFilters(
            database.Table<TEntity>(),
            query);

        return await table.CountAsync() > 0;
    }

    private static AsyncTableQuery<TEntity> ApplyFilters(
        AsyncTableQuery<TEntity> table,
        Query<TEntity> query)
    {
        foreach (var filter in query.Filters)
        {
            if (filter.Operator == QueryOperator.In)
                continue;

            table = table.Where(CreateExpression(filter));
        }

        return table;
    }

    private static AsyncTableQuery<TEntity> ApplyOrder(
        AsyncTableQuery<TEntity> table,
        Query<TEntity> query)
    {
        if (query.Order is null)
            return table;

        var property = typeof(TEntity)
            .GetProperty(query.Order.Field);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property '{query.Order.Field}' was not found on '{typeof(TEntity).Name}'.");
        }

        var parameter = Expression.Parameter(
            typeof(TEntity),
            "x");

        var member = Expression.Property(
            parameter,
            property);

        var lambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(
                typeof(TEntity),
                property.PropertyType),
            member,
            parameter);

        var method = query.Order.Descending
            ? "OrderByDescending"
            : "OrderBy";

        var orderMethod = typeof(Queryable)
            .GetMethods()
            .Single(x =>
                x.Name == method &&
                x.GetParameters().Length == 2);

        var genericMethod = orderMethod.MakeGenericMethod(
            typeof(TEntity),
            property.PropertyType);

        return (AsyncTableQuery<TEntity>)
            genericMethod.Invoke(
                null,
                [table, lambda])!;
    }

    private static Expression<Func<TEntity, bool>> CreateExpression(
    QueryFilter filter)
    {
        var parameter = Expression.Parameter(
            typeof(TEntity),
            "x");

        var property = typeof(TEntity)
            .GetProperty(filter.Field);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property '{filter.Field}' was not found on '{typeof(TEntity).Name}'.");
        }

        var member = Expression.Property(
            parameter,
            property);

        Expression body = filter.Operator switch
        {
            QueryOperator.In =>
                CreateInExpression(
                    member,
                    property.PropertyType,
                    filter.Value),

            QueryOperator.Equal =>
                Expression.Equal(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            QueryOperator.NotEqual =>
                Expression.NotEqual(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            QueryOperator.GreaterThan =>
                Expression.GreaterThan(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            QueryOperator.GreaterThanOrEqual =>
                Expression.GreaterThanOrEqual(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            QueryOperator.LessThan =>
                Expression.LessThan(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            QueryOperator.LessThanOrEqual =>
                Expression.LessThanOrEqual(
                    member,
                    Expression.Constant(
                        ConvertValue(filter.Value, property.PropertyType),
                        property.PropertyType)),

            _ => throw new NotSupportedException(
                $"Operator '{filter.Operator}' is not supported by SQLite.")
        };

        return Expression.Lambda<Func<TEntity, bool>>(
            body,
            parameter);
    }

    private static object? ConvertValue(
        object? value,
        Type targetType)
    {
        if (value is null)
            return null;

        var underlyingType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (underlyingType.IsInstanceOfType(value))
            return value;

        if (underlyingType.IsEnum)
            return Enum.Parse(
                underlyingType,
                value.ToString()!,
                true);

        return Convert.ChangeType(
            value,
            underlyingType);
    }

    private static Expression CreateInExpression(
        Expression member,
        Type propertyType,
        object? value)
    {
        if (value is not IEnumerable values)
            throw new ArgumentException(
                "The value of an 'In' filter must be a collection.");

        var convertedValues = values
            .Cast<object?>()
            .Select(x => ConvertValue(x, propertyType))
            .ToArray();

        if (convertedValues.Length == 0)
            return Expression.Constant(false);

        var array = Array.CreateInstance(
            propertyType,
            convertedValues.Length);

        for (var i = 0; i < convertedValues.Length; i++)
            array.SetValue(convertedValues[i], i);

        return Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [propertyType],
            Expression.Constant(array),
            member);
    }

    private async Task<List<TEntity>> ExecuteInQueryAsync(
        Query<TEntity> query)
    {
        var tableName = typeof(TEntity).Name;
        var conditions = new List<string>();
        var parameters = new List<object>();

        foreach (var filter in query.Filters)
        {
            var property = typeof(TEntity).GetProperty(filter.Field)
                ?? throw new InvalidOperationException(
                    $"Property '{filter.Field}' was not found on '{typeof(TEntity).Name}'.");

            if (filter.Operator == QueryOperator.In)
            {
                if (filter.Value is not IEnumerable values)
                    throw new ArgumentException(
                        "The value of an 'In' filter must be a collection.");

                var convertedValues = values
                    .Cast<object?>()
                    .Select(x => ConvertValue(x, property.PropertyType))
                    .ToArray();

                if (convertedValues.Length == 0)
                    return [];

                var placeholders = string.Join(
                    ", ",
                    Enumerable.Repeat("?", convertedValues.Length));

                conditions.Add(
                    $"\"{filter.Field}\" IN ({placeholders})");

                parameters.AddRange(convertedValues!);

                continue;
            }

            var value = ConvertValue(
                filter.Value,
                property.PropertyType);

            var sqlOperator = filter.Operator switch
            {
                QueryOperator.Equal => "=",
                QueryOperator.NotEqual => "<>",
                QueryOperator.GreaterThan => ">",
                QueryOperator.GreaterThanOrEqual => ">=",
                QueryOperator.LessThan => "<",
                QueryOperator.LessThanOrEqual => "<=",

                _ => throw new NotSupportedException(
                    $"Operator '{filter.Operator}' is not supported by SQLite.")
            };

            conditions.Add(
                $"\"{filter.Field}\" {sqlOperator} ?");

            parameters.Add(value!);
        }

        var sql = new StringBuilder(
            $"SELECT * FROM \"{tableName}\"");

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", conditions));
        }

        if (query.Order is not null)
        {
            sql.Append(
                $" ORDER BY \"{query.Order.Field}\" " +
                (query.Order.Descending ? "DESC" : "ASC"));
        }

        sql.Append($" LIMIT {query.PageSize}");
        sql.Append($" OFFSET {query.Skip}");

        return await database.QueryAsync<TEntity>(
            sql.ToString(),
            parameters.ToArray());
    }

    private static bool HasInFilter(Query<TEntity> query) =>
        query.Filters.Any(x => x.Operator == QueryOperator.In);

}
