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
                    definition += " PRIMARY KEY";

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
            await using var connection = await database.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);

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

    private static string GetSqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int16 => "SMALLINT",
            TypeCode.Int32 => "INTEGER",
            TypeCode.Int64 => "BIGINT",
            TypeCode.Byte => "SMALLINT",
            TypeCode.Boolean => "BOOLEAN",
            TypeCode.Decimal => "NUMERIC",
            TypeCode.Double => "DOUBLE PRECISION",
            TypeCode.Single => "REAL",
            TypeCode.DateTime => "TIMESTAMP",
            TypeCode.String => "TEXT",
            _ => "TEXT"
        };
    }
}