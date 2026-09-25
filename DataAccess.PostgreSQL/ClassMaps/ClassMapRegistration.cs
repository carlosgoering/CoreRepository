using DataAccess.Core.Metadata;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DataAccess.PostgreSQL.ClassMaps;

internal static class ClassMapRegistration
{
    public static async Task Register<TEntity>(
        NpgsqlDataSource database,
        ILogger logger)
        where TEntity : class, new()
    {
        var type = typeof(TEntity);
        var tableName = type.Name;

        logger.LogInformation(
            $"[PostgreSQL] Registering table '{tableName}'.");

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        if (primaryKey is null)
        {
            var exception = new InvalidOperationException(
                $"Entity '{type.Name}' must define a [PrimaryKey].");

            logger.LogError(
                exception,
                $"[PostgreSQL] Failed to register table '{tableName}'.");

            throw exception;
        }

        var columns = type
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite)
            .Select(x =>
            {
                var sqlType = GetSqlType(x.PropertyType);

                var definition =
                    $"\"{x.Name}\" {sqlType}";

                if (x == primaryKey)
                {
                    definition += " PRIMARY KEY";
                }
                else if (!IsNullable(x.PropertyType))
                {
                    definition += " NOT NULL";
                }

                return definition;
            })
            .ToArray();

        var sql = $"""
            CREATE TABLE IF NOT EXISTS "{tableName}"
            (
                {string.Join(",\n", columns)}
            );
            """;

        logger.LogDebug(
            $"[PostgreSQL] SQL for '{tableName}':{Environment.NewLine}{sql}");

        try
        {
            await using var connection =
                await database.OpenConnectionAsync();

            await using var command =
                new NpgsqlCommand(sql, connection);

            await command.ExecuteNonQueryAsync();

            logger.LogInformation(
                $"[PostgreSQL] Table '{tableName}' created/verified successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                $"[PostgreSQL] Failed to create table '{tableName}'.");

            throw;
        }
    }

    private static bool IsNullable(Type type)
    {
        return Nullable.GetUnderlyingType(type) is not null;
    }

    private static string GetSqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => "SMALLINT",
            TypeCode.SByte => "SMALLINT",
            TypeCode.Int16 => "SMALLINT",
            TypeCode.UInt16 => "INTEGER",
            TypeCode.Int32 => "INTEGER",
            TypeCode.UInt32 => "BIGINT",
            TypeCode.Int64 => "BIGINT",
            TypeCode.UInt64 => "NUMERIC",

            TypeCode.Boolean => "BOOLEAN",
            TypeCode.Decimal => "NUMERIC",
            TypeCode.Double => "DOUBLE PRECISION",
            TypeCode.Single => "REAL",
            TypeCode.DateTime => "TIMESTAMP WITH TIME ZONE",
            TypeCode.String => "TEXT",
            TypeCode.Object when type == typeof(Guid) => "UUID",

            _ => "TEXT"
        };
    }
}