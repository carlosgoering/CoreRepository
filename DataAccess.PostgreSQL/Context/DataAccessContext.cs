using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Metadata;
using DataAccess.PostgreSQL.ClassMaps;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Collections;
using System.Text;

namespace DataAccess.PostgreSQL.Context;

internal sealed class DataAccessContext<TEntity> :
    IDataAccessContext<TEntity>
    where TEntity : class, new()
{
    private readonly NpgsqlDataSource database;
    private readonly ILogger<DataAccessContext<TEntity>> logger;
    private readonly Task initialization;

    public DataAccessContext(
        NpgsqlDataSource database,
        ILogger<DataAccessContext<TEntity>> logger)
    {
        this.database = database;
        this.logger = logger;

        initialization = ClassMapRegistration.Register<TEntity>(
            database,
            logger);
    }

    public async Task InsertAsync(TEntity entity)
    {
        await initialization;

        var properties = typeof(TEntity)
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite)
            .ToArray();

        var tableName = typeof(TEntity).Name;

        var columns = string.Join(
            ", ",
            properties.Select(x => $"\"{x.Name}\""));

        var parameters = string.Join(
            ", ",
            properties.Select((_, index) => $"@p{index}"));

        var sql = $"""
            INSERT INTO "{tableName}"
            ({columns})
            VALUES ({parameters});
            """;

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        for (var i = 0; i < properties.Length; i++)
        {
            command.Parameters.Add(
                CreateParameter(
                    $"@p{i}",
                    properties[i].GetValue(entity),
                    properties[i].PropertyType));
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(TEntity entity)
    {
        await initialization;

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var primaryKeyValue =
            EntityMetadata.GetPrimaryKeyValue(entity);

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
            properties.Select(
                (x, index) => $"\"{x.Name}\" = @p{index}"));

        var tableName = typeof(TEntity).Name;

        var sql = $"""
            UPDATE "{tableName}"
            SET {setClause}
            WHERE "{primaryKey.Name}" = @primaryKey;
            """;

        logger.LogDebug(
            $"[PostgreSQL] UPDATE '{tableName}' " +
            $"WHERE '{primaryKey.Name}' = '{primaryKeyValue}'");

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        for (var i = 0; i < properties.Length; i++)
        {
            command.Parameters.Add(
                CreateParameter(
                    $"@p{i}",
                    properties[i].GetValue(entity),
                    properties[i].PropertyType));
        }

        command.Parameters.Add(
            CreateParameter(
                "@primaryKey",
                primaryKeyValue,
                primaryKey.PropertyType));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(TEntity entity)
    {
        await initialization;

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var primaryKeyValue =
            EntityMetadata.GetPrimaryKeyValue(entity);

        if (primaryKeyValue is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has a null primary key.");
        }

        var tableName = typeof(TEntity).Name;

        var sql = $"""
            DELETE FROM "{tableName}"
            WHERE "{primaryKey.Name}" = @primaryKey;
            """;

        logger.LogDebug(
            $"[PostgreSQL] DELETE FROM '{tableName}' " +
            $"WHERE '{primaryKey.Name}' = '{primaryKeyValue}'");

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.Add(
            CreateParameter(
                "@primaryKey",
                primaryKeyValue,
                primaryKey.PropertyType));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Query<TEntity> query)
    {
        await initialization;

        var commandDefinition = BuildQuery(
            query,
            includePaging: false);

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            CreateCommand(connection, commandDefinition);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapEntity(reader);
    }

    public async Task<IReadOnlyCollection<TEntity>> SelectAsync(
        Query<TEntity> query)
    {
        await initialization;

        var commandDefinition = BuildQuery(
            query,
            includePaging: true);

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            CreateCommand(connection, commandDefinition);

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
        await initialization;

        var countQuery = BuildCountQuery(query);

        await using var connection =
            await database.OpenConnectionAsync();

        await using var countCommand =
            CreateCommand(connection, countQuery);

        var total = Convert.ToInt64(
            await countCommand.ExecuteScalarAsync());

        var commandDefinition = BuildQuery(
            query,
            includePaging: true);

        await using var command =
            CreateCommand(connection, commandDefinition);

        await using var reader =
            await command.ExecuteReaderAsync();

        var items = new List<TEntity>();

        while (await reader.ReadAsync())
            items.Add(MapEntity(reader));

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

        var definition = BuildCountQuery(query);

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            CreateCommand(connection, definition);

        return Convert.ToInt64(
            await command.ExecuteScalarAsync());
    }

    public async Task<bool> ExistsAsync(
        Query<TEntity> query)
    {
        await initialization;

        var definition = BuildExistsQuery(query);

        await using var connection =
            await database.OpenConnectionAsync();

        await using var command =
            CreateCommand(connection, definition);

        return Convert.ToBoolean(
            await command.ExecuteScalarAsync());
    }

    private static QueryDefinition BuildQuery(
        Query<TEntity> query,
        bool includePaging)
    {
        var tableName = typeof(TEntity).Name;

        var conditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        var parameterIndex = 0;

        foreach (var filter in query.Filters)
        {
            var property = typeof(TEntity)
                .GetProperty(filter.Field)
                ?? throw new InvalidOperationException(
                    $"Property '{filter.Field}' was not found on '{typeof(TEntity).Name}'.");

            if (filter.Operator == QueryOperator.In)
            {
                if (filter.Value is not IEnumerable values)
                {
                    throw new ArgumentException(
                        "The value of an 'In' filter must be a collection.");
                }

                var convertedValues = values
                    .Cast<object?>()
                    .Select(x =>
                        ConvertValue(x, property.PropertyType))
                    .ToArray();

                if (convertedValues.Length == 0)
                {
                    conditions.Add("FALSE");
                    continue;
                }

                var placeholders = new List<string>();

                foreach (var value in convertedValues)
                {
                    var parameterName = $"@p{parameterIndex++}";

                    placeholders.Add(parameterName);

                    parameters.Add(
                        CreateParameter(
                            parameterName,
                            value,
                            property.PropertyType));
                }

                conditions.Add(
                    $"\"{filter.Field}\" IN ({string.Join(", ", placeholders)})");

                continue;
            }

            var convertedValue =
                ConvertValue(
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
                    $"Operator '{filter.Operator}' is not supported by PostgreSQL.")
            };

            var parameter = $"@p{parameterIndex++}";

            conditions.Add(
                $"\"{filter.Field}\" {sqlOperator} {parameter}");

            parameters.Add(
                CreateParameter(
                    parameter,
                    convertedValue,
                    property.PropertyType));
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
            var property = typeof(TEntity)
                .GetProperty(query.Order.Field)
                ?? throw new InvalidOperationException(
                    $"Property '{query.Order.Field}' was not found on '{typeof(TEntity).Name}'.");

            sql.Append(
                $" ORDER BY \"{property.Name}\" " +
                (query.Order.Descending ? "DESC" : "ASC"));
        }

        if (includePaging)
        {
            sql.Append(" LIMIT @pageSize");
            sql.Append(" OFFSET @skip");

            parameters.Add(
                CreateParameter(
                    "@pageSize",
                    query.PageSize,
                    typeof(int)));

            parameters.Add(
                CreateParameter(
                    "@skip",
                    query.Skip,
                    typeof(int)));
        }

        return new QueryDefinition(
            sql.ToString(),
            parameters);
    }

    private static QueryDefinition BuildCountQuery(
        Query<TEntity>? query)
    {
        var tableName = typeof(TEntity).Name;

        if (query is null)
        {
            return new QueryDefinition(
                $"SELECT COUNT(*) FROM \"{tableName}\"",
                []);
        }

        var definition = BuildQuery(
            query with
            {
                Order = null
            },
            includePaging: false);

        var sql = definition.Sql
            .Replace(
                $"SELECT * FROM \"{tableName}\"",
                $"SELECT COUNT(*) FROM \"{tableName}\"");

        return new QueryDefinition(
            sql,
            definition.Parameters);
    }

    private static QueryDefinition BuildExistsQuery(
        Query<TEntity> query)
    {
        var count = BuildCountQuery(query);

        var sql = $"""
            SELECT EXISTS(
                {count.Sql}
            );
            """;

        return new QueryDefinition(
            sql,
            count.Parameters);
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        QueryDefinition definition)
    {
        var command = new NpgsqlCommand(
            definition.Sql,
            connection);

        command.Parameters.AddRange(
            definition.Parameters.ToArray());

        return command;
    }

    private static TEntity MapEntity(
        NpgsqlDataReader reader)
    {
        var entity = new TEntity();

        foreach (var property in typeof(TEntity)
                     .GetProperties()
                     .Where(x => x.CanRead && x.CanWrite))
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
                ConvertValue(value, property.PropertyType));
        }

        return entity;
    }

    private static NpgsqlParameter CreateParameter(
        string name,
        object? value,
        Type targetType)
    {
        var parameterType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (parameterType.IsEnum)
            parameterType = Enum.GetUnderlyingType(parameterType);

        var convertedValue =
            ConvertValue(value, targetType);

        return new NpgsqlParameter
        {
            ParameterName = name,
            Value = convertedValue ?? DBNull.Value,
            NpgsqlDbType = GetNpgsqlDbType(parameterType),
            IsNullable = Nullable.GetUnderlyingType(targetType) is not null
        };
    }

    private static NpgsqlDbType GetNpgsqlDbType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => NpgsqlDbType.Smallint,
            TypeCode.SByte => NpgsqlDbType.Smallint,
            TypeCode.Int16 => NpgsqlDbType.Smallint,
            TypeCode.UInt16 => NpgsqlDbType.Integer,
            TypeCode.Int32 => NpgsqlDbType.Integer,
            TypeCode.UInt32 => NpgsqlDbType.Bigint,
            TypeCode.Int64 => NpgsqlDbType.Bigint,
            TypeCode.UInt64 => NpgsqlDbType.Numeric,

            TypeCode.Boolean => NpgsqlDbType.Boolean,
            TypeCode.Decimal => NpgsqlDbType.Numeric,
            TypeCode.Double => NpgsqlDbType.Double,
            TypeCode.Single => NpgsqlDbType.Real,
            TypeCode.DateTime => NpgsqlDbType.TimestampTz,
            TypeCode.String => NpgsqlDbType.Text,

            _ => throw new NotSupportedException(
                $"Type '{type.FullName}' is not supported by PostgreSQL.")
        };
    }

    private static object? ConvertValue(
        object? value,
        Type targetType)
    {
        if (value is null || value is DBNull)
            return null;

        var underlyingType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (underlyingType.IsEnum)
        {
            return Convert.ChangeType(
                value,
                Enum.GetUnderlyingType(underlyingType));
        }

        if (underlyingType.IsInstanceOfType(value))
            return value;

        return Convert.ChangeType(
            value,
            underlyingType);
    }

    private sealed record QueryDefinition(
        string Sql,
        List<NpgsqlParameter> Parameters);
}