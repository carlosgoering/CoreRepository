using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Metadata;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using DataAccess.MySQL.ClassMaps;

namespace DataAccess.MySQL.Context;

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
            new MySqlConnection(connectionString);

        await ClassMapRegistration.Register<TEntity>(
            connection,
            logger);
    }

    private async Task<MySqlConnection> CreateConnectionAsync()
    {
        await initialization;

        var connection =
            new MySqlConnection(connectionString);

        await connection.OpenAsync();

        return connection;
    }

    public async Task InsertAsync(TEntity entity)
    {
        await using var connection =
            await CreateConnectionAsync();

        var properties = typeof(TEntity)
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite)
            .ToArray();

        var columns = string.Join(
            ", ",
            properties.Select(x => $"`{x.Name}`"));

        var parameters = string.Join(
            ", ",
            properties.Select((_, index) => $"@p{index}"));

        var sql = $"""
            INSERT INTO `{typeof(TEntity).Name}`
            ({columns})
            VALUES ({parameters});
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        for (var index = 0; index < properties.Length; index++)
        {
            command.Parameters.AddWithValue(
                $"@p{index}",
                properties[index].GetValue(entity) ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(TEntity entity)
    {
        await using var connection =
            await CreateConnectionAsync();

        var primaryKey =
            EntityMetadata.GetPrimaryKey<TEntity>();

        var properties = typeof(TEntity)
            .GetProperties()
            .Where(x =>
                x.CanRead &&
                x.CanWrite &&
                x != primaryKey)
            .ToArray();

        var setClause = string.Join(
            ", ",
            properties.Select(
                (x, index) => $"`{x.Name}` = @p{index}"));

        var sql = $"""
            UPDATE `{typeof(TEntity).Name}`
            SET {setClause}
            WHERE `{primaryKey.Name}` = @primaryKey;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        for (var index = 0; index < properties.Length; index++)
        {
            command.Parameters.AddWithValue(
                $"@p{index}",
                properties[index].GetValue(entity) ?? DBNull.Value);
        }

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
            DELETE FROM `{typeof(TEntity).Name}`
            WHERE `{primaryKey.Name}` = @primaryKey;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

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

        var commandData = BuildSelectQuery(query);

        await using var command =
            new MySqlCommand(commandData.Sql, connection);

        AddParameters(command, commandData.Parameters);

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

        var commandData = BuildSelectQuery(query);

        await using var command =
            new MySqlCommand(commandData.Sql, connection);

        AddParameters(command, commandData.Parameters);

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

        var commandData = BuildSelectQuery(
            query,
            includePaging: true);

        await using var command =
            new MySqlCommand(commandData.Sql, connection);

        AddParameters(command, commandData.Parameters);

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

        var commandData = BuildCountQuery(query);

        await using var command =
            new MySqlCommand(commandData.Sql, connection);

        AddParameters(command, commandData.Parameters);

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt64(result);
    }

    public async Task<bool> ExistsAsync(
        Query<TEntity> query)
    {
        await using var connection =
            await CreateConnectionAsync();

        var commandData = BuildExistsQuery(query);

        await using var command =
            new MySqlCommand(commandData.Sql, connection);

        AddParameters(command, commandData.Parameters);

        var result =
            await command.ExecuteScalarAsync();

        return Convert.ToBoolean(result);
    }

    private (
        string Sql,
        List<MySqlParameter> Parameters)
        BuildSelectQuery(
            Query<TEntity> query,
            bool includePaging = false)
    {
        var parameters = new List<MySqlParameter>();

        var sql =
            $"SELECT * FROM `{typeof(TEntity).Name}`";

        var where = BuildWhereClause(
            query,
            parameters);

        if (!string.IsNullOrWhiteSpace(where))
            sql += $" WHERE {where}";

        if (query.Order is not null)
        {
            sql +=
                $" ORDER BY `{query.Order.Field}` " +
                (query.Order.Descending ? "DESC" : "ASC");
        }

        if (includePaging)
        {
            sql +=
                $" LIMIT {query.PageSize} " +
                $"OFFSET {query.Skip}";
        }
        else
        {
            sql += " LIMIT 1";
        }

        return (sql, parameters);
    }

    private (
        string Sql,
        List<MySqlParameter> Parameters)
        BuildCountQuery(
            Query<TEntity>? query)
    {
        var parameters = new List<MySqlParameter>();

        var sql =
            $"SELECT COUNT(*) FROM `{typeof(TEntity).Name}`";

        if (query is not null)
        {
            var where = BuildWhereClause(
                query,
                parameters);

            if (!string.IsNullOrWhiteSpace(where))
                sql += $" WHERE {where}";
        }

        return (sql, parameters);
    }

    private (
        string Sql,
        List<MySqlParameter> Parameters)
        BuildExistsQuery(
            Query<TEntity> query)
    {
        var parameters = new List<MySqlParameter>();

        var where = BuildWhereClause(
            query,
            parameters);

        var sql =
            $"SELECT EXISTS(" +
            $"SELECT 1 FROM `{typeof(TEntity).Name}`";

        if (!string.IsNullOrWhiteSpace(where))
            sql += $" WHERE {where}";

        sql += ")";

        return (sql, parameters);
    }

    private string BuildWhereClause(
        Query<TEntity> query,
        List<MySqlParameter> parameters)
    {
        if (query.Filters.Count == 0)
            return string.Empty;

        var conditions = new List<string>();

        for (var index = 0;
             index < query.Filters.Count;
             index++)
        {
            var filter = query.Filters.ElementAt(index);

            if (filter.Operator == QueryOperator.In)
            {
                if (filter.Value is not System.Collections.IEnumerable values)
                    throw new InvalidOperationException(
                        $"Filter '{filter.Field}' requires an enumerable value.");

                var placeholders = new List<string>();
                var valueIndex = 0;

                foreach (var value in values)
                {
                    var parameterName =
                        $"@p{parameters.Count}";

                    placeholders.Add(parameterName);

                    parameters.Add(
                        new MySqlParameter(
                            parameterName,
                            value ?? DBNull.Value));

                    valueIndex++;
                }

                if (placeholders.Count == 0)
                {
                    conditions.Add("1 = 0");
                    continue;
                }

                conditions.Add(
                    $"`{filter.Field}` IN " +
                    $"({string.Join(", ", placeholders)})");

                continue;
            }

            var name = $"@p{parameters.Count}";

            conditions.Add(
                $"`{filter.Field}` " +
                GetOperator(filter.Operator) +
                $" {name}");

            parameters.Add(
                new MySqlParameter(
                    name,
                    ConvertValue(
                        filter.Value,
                        typeof(TEntity)
                            .GetProperty(filter.Field)!
                            .PropertyType)
                    ?? DBNull.Value));
        }

        return string.Join(" AND ", conditions);
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
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!);

        if (type == typeof(DateTime))
            return value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(value.ToString()!);

        if (type == typeof(DateTimeOffset))
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(value.ToString()!);

        return Convert.ChangeType(value, type);
    }

    private static void AddParameters(
        MySqlCommand command,
        IEnumerable<MySqlParameter> parameters)
    {
        command.Parameters.AddRange(
            parameters.ToArray());
    }

    private static TEntity MapEntity(
        MySqlDataReader reader)
    {
        var entity = new TEntity();

        foreach (var property in typeof(TEntity)
            .GetProperties()
            .Where(x => x.CanWrite))
        {
            var ordinal = reader.GetOrdinal(property.Name);

            if (reader.IsDBNull(ordinal))
            {
                property.SetValue(entity, null);
                continue;
            }

            var value = reader.GetValue(ordinal);

            property.SetValue(
                entity,
                ConvertValue(
                    value,
                    property.PropertyType));
        }

        return entity;
    }
}