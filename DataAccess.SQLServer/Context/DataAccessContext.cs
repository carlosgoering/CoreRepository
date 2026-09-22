using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Metadata;
using DataAccess.SqlServer.ClassMap;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Data;
using System.Reflection;

namespace DataAccess.SqlServer.Context;

internal sealed class DataAccessContext<TEntity>
    : IDataAccessContext<TEntity>
    where TEntity : class, new()
{
    private readonly string connectionString;
    private readonly ILogger<DataAccessContext<TEntity>> logger;

    private readonly Task initialization;

    public DataAccessContext(
        string connectionString,
        ILogger<DataAccessContext<TEntity>> logger)
    {
        this.connectionString = connectionString;
        this.logger = logger;

        initialization = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await using var connection =
            new SqlConnection(connectionString);

        await ClassMapRegistration.Register<TEntity>(
            connection,
            logger);
    }

    private async Task<SqlConnection> CreateConnectionAsync()
    {
        await initialization;

        var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync();

        return connection;
    }

    public async Task InsertAsync(TEntity entity)
    {
        await using var connection =
            await CreateConnectionAsync();

        var properties = GetWritableProperties();

        var columns = string.Join(
            ", ",
            properties.Select(x => $"[{x.Name}]"));

        var parameters = string.Join(
            ", ",
            properties.Select((_, index) => $"@p{index}"));

        var sql = $"""
            INSERT INTO [{typeof(TEntity).Name}]
            ({columns})
            VALUES ({parameters});
            """;

        await using var command =
            new SqlCommand(sql, connection);

        AddEntityParameters(
            command,
            properties,
            entity);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(TEntity entity)
    {
        await using var connection =
            await CreateConnectionAsync();

        var primaryKey =
            EntityMetadata.GetPrimaryKey<TEntity>();

        var properties = GetWritableProperties()
            .Where(x => x != primaryKey)
            .ToArray();

        var setClause = string.Join(
            ", ",
            properties.Select(
                (x, index) =>
                    $"[{x.Name}] = @p{index}"));

        var sql = $"""
            UPDATE [{typeof(TEntity).Name}]
            SET {setClause}
            WHERE [{primaryKey.Name}] = @primaryKey;
            """;

        await using var command =
            new SqlCommand(sql, connection);

        AddEntityParameters(
            command,
            properties,
            entity);

        command.Parameters.AddWithValue(
            "@primaryKey",
            EntityMetadata.GetPrimaryKeyValue(entity)
                ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(TEntity entity)
    {
        await using var connection =
            await CreateConnectionAsync();

        var primaryKey =
            EntityMetadata.GetPrimaryKey<TEntity>();

        var sql = $"""
            DELETE FROM [{typeof(TEntity).Name}]
            WHERE [{primaryKey.Name}] = @primaryKey;
            """;

        await using var command =
            new SqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@primaryKey",
            EntityMetadata.GetPrimaryKeyValue(entity)
                ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Query<TEntity> query)
    {
        await using var connection =
            await CreateConnectionAsync();

        var commandData =
            BuildSelectQuery(query, true);

        await using var command =
            new SqlCommand(commandData.Sql, connection);

        AddParameters(
            command,
            commandData.Parameters);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapEntity(reader);
    }

    public async Task<IReadOnlyCollection<TEntity>> SelectAsync(
        Query<TEntity> query)
    {
        await using var connection =
            await CreateConnectionAsync();

        var commandData =
            BuildSelectQuery(query);

        await using var command =
            new SqlCommand(commandData.Sql, connection);

        AddParameters(
            command,
            commandData.Parameters);

        await using var reader =
            await command.ExecuteReaderAsync();

        var result = new List<TEntity>();

        while (await reader.ReadAsync())
            result.Add(MapEntity(reader));

        return result;
    }

    public async Task<PagedResult<TEntity>> SelectPagedAsync(
        Query<TEntity> query)
    {
        var total = await CountAsync(query);

        await using var connection =
            await CreateConnectionAsync();

        var commandData =
            BuildSelectQuery(query, false, true);

        await using var command =
            new SqlCommand(commandData.Sql, connection);

        AddParameters(
            command,
            commandData.Parameters);

        await using var reader =
            await command.ExecuteReaderAsync();

        var result = new List<TEntity>();

        while (await reader.ReadAsync())
            result.Add(MapEntity(reader));

        return new PagedResult<TEntity>
        {
            Items = result,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<long> CountAsync(
        Query<TEntity>? query = null)
    {
        await using var connection =
            await CreateConnectionAsync();

        var commandData =
            BuildCountQuery(query);

        await using var command =
            new SqlCommand(commandData.Sql, connection);

        AddParameters(
            command,
            commandData.Parameters);

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt64(result);
    }

    public async Task<bool> ExistsAsync(
        Query<TEntity> query)
    {
        await using var connection =
            await CreateConnectionAsync();

        var commandData =
            BuildExistsQuery(query);

        await using var command =
            new SqlCommand(commandData.Sql, connection);

        AddParameters(
            command,
            commandData.Parameters);

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToBoolean(result);
    }

    private (
        string Sql,
        List<SqlParameter> Parameters)
        BuildSelectQuery(
            Query<TEntity> query,
            bool firstOnly = false,
            bool includePaging = false)
    {
        var parameters =
            new List<SqlParameter>();

        var sql =
            $"SELECT ";

        if (firstOnly)
            sql += "TOP 1 ";

        sql +=
            $"* FROM [{typeof(TEntity).Name}]";

        var where =
            BuildWhereClause(
                query,
                parameters);

        if (!string.IsNullOrWhiteSpace(where))
            sql += $" WHERE {where}";

        if (query.Order is not null)
        {
            sql +=
                $" ORDER BY [{query.Order.Field}] " +
                (query.Order.Descending
                    ? "DESC"
                    : "ASC");
        }

        if (includePaging)
        {
            if (query.Order is null)
                sql +=
                    $" ORDER BY (SELECT NULL)";

            sql +=
                $" OFFSET {query.Skip} ROWS " +
                $"FETCH NEXT {query.PageSize} ROWS ONLY";
        }

        return (sql, parameters);
    }

    private (
        string Sql,
        List<SqlParameter> Parameters)
        BuildCountQuery(
            Query<TEntity>? query)
    {
        var parameters =
            new List<SqlParameter>();

        var sql =
            $"SELECT COUNT_BIG(*) " +
            $"FROM [{typeof(TEntity).Name}]";

        if (query is not null)
        {
            var where =
                BuildWhereClause(
                    query,
                    parameters);

            if (!string.IsNullOrWhiteSpace(where))
                sql += $" WHERE {where}";
        }

        return (sql, parameters);
    }

    private (
        string Sql,
        List<SqlParameter> Parameters)
        BuildExistsQuery(
            Query<TEntity> query)
    {
        var parameters =
            new List<SqlParameter>();

        var where =
            BuildWhereClause(
                query,
                parameters);

        var sql =
            $"SELECT CAST(CASE WHEN EXISTS (" +
            $"SELECT 1 " +
            $"FROM [{typeof(TEntity).Name}]";

        if (!string.IsNullOrWhiteSpace(where))
            sql += $" WHERE {where}";

        sql +=
            ") THEN 1 ELSE 0 END AS BIT);";

        return (sql, parameters);
    }

    private string BuildWhereClause(
        Query<TEntity> query,
        List<SqlParameter> parameters)
    {
        if (query.Filters.Count == 0)
            return string.Empty;

        var conditions =
            new List<string>();

        foreach (var filter in query.Filters)
        {
            if (filter.Operator == QueryOperator.In)
            {
                if (filter.Value is not IEnumerable values)
                {
                    throw new InvalidOperationException(
                        $"Filter '{filter.Field}' requires an enumerable value.");
                }

                var placeholders =
                    new List<string>();

                foreach (var value in values)
                {
                    var parameterName =
                        $"@p{parameters.Count}";

                    placeholders.Add(parameterName);

                    parameters.Add(
                        CreateParameter(
                            parameterName,
                            value,
                            GetPropertyType(filter.Field)));
                }

                if (placeholders.Count == 0)
                {
                    conditions.Add("1 = 0");
                    continue;
                }

                conditions.Add(
                    $"[{filter.Field}] IN " +
                    $"({string.Join(", ", placeholders)})");

                continue;
            }

            var name =
                $"@p{parameters.Count}";

            conditions.Add(
                $"[{filter.Field}] " +
                $"{GetOperator(filter.Operator)} " +
                $"{name}");

            parameters.Add(
                CreateParameter(
                    name,
                    filter.Value,
                    GetPropertyType(filter.Field)));
        }

        return string.Join(
            " AND ",
            conditions);
    }

    private Type GetPropertyType(
        string propertyName)
    {
        return typeof(TEntity)
            .GetProperty(propertyName)
            ?.PropertyType
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' " +
                $"does not exist on '{typeof(TEntity).Name}'.");
    }

    private static string GetOperator(
        QueryOperator operation)
    {
        return operation switch
        {
            QueryOperator.Equal => "=",
            QueryOperator.NotEqual => "<>",
            QueryOperator.GreaterThan => ">",
            QueryOperator.GreaterThanOrEqual => ">=",
            QueryOperator.LessThan => "<",
            QueryOperator.LessThanOrEqual => "<=",

            _ => throw new NotSupportedException(
                $"Operator '{operation}' is not supported.")
        };
    }

    private static SqlParameter CreateParameter(
        string name,
        object? value,
        Type targetType)
    {
        var converted =
            ConvertValue(
                value,
                targetType);

        return new SqlParameter(
            name,
            converted ?? DBNull.Value);
    }

    private static object? ConvertValue(
        object? value,
        Type targetType)
    {
        if (value is null)
            return null;

        var type =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (type.IsEnum)
            return Enum.Parse(
                type,
                value.ToString()!,
                true);

        if (type == typeof(Guid))
        {
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!);
        }

        if (type == typeof(DateTime))
        {
            return value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(value.ToString()!);
        }

        if (type == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(value.ToString()!);
        }

        return Convert.ChangeType(
            value,
            type);
    }

    private static PropertyInfo[] GetWritableProperties()
    {
        return typeof(TEntity)
            .GetProperties()
            .Where(x =>
                x.CanRead &&
                x.CanWrite)
            .ToArray();
    }

    private static void AddEntityParameters(
        SqlCommand command,
        IEnumerable<PropertyInfo> properties,
        TEntity entity)
    {
        var index = 0;

        foreach (var property in properties)
        {
            command.Parameters.Add(
                CreateParameter(
                    $"@p{index}",
                    property.GetValue(entity),
                    property.PropertyType));

            index++;
        }
    }

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<SqlParameter> parameters)
    {
        command.Parameters.AddRange(
            parameters.ToArray());
    }

    private static TEntity MapEntity(
        SqlDataReader reader)
    {
        var entity = new TEntity();

        foreach (var property in typeof(TEntity)
            .GetProperties()
            .Where(x => x.CanWrite))
        {
            var ordinal =
                reader.GetOrdinal(property.Name);

            if (reader.IsDBNull(ordinal))
            {
                property.SetValue(
                    entity,
                    null);

                continue;
            }

            var value =
                reader.GetValue(ordinal);

            property.SetValue(
                entity,
                ConvertValue(
                    value,
                    property.PropertyType));
        }

        return entity;
    }
}