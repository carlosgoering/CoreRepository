using DataAccess.Core.Metadata;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace DataAccess.MySQL.ClassMaps;

internal static class ClassMapRegistration
{
    public static async Task Register<TEntity>(
        MySqlConnection connection,
        ILogger logger)
        where TEntity : class, new()
    {
        var type = typeof(TEntity);
        var tableName = type.Name;

        logger.LogInformation(
            "[MySQL] Registering table '{TableName}'.",
            tableName);

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var columns = type
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite)
            .Select(x =>
            {
                var sqlType = GetSqlType(x.PropertyType);

                var definition =
                    $"`{x.Name}` {sqlType}";

                if (x == primaryKey)
                    definition += " PRIMARY KEY";

                return definition;
            })
            .ToArray();

        var sql = $"""
            CREATE TABLE IF NOT EXISTS `{tableName}`
            (
                {string.Join(",\n", columns)}
            );
            """;

        logger.LogDebug(
            "[MySQL] SQL for '{TableName}':{NewLine}{Sql}",
            tableName,
            Environment.NewLine,
            sql);

        try
        {
            await connection.OpenAsync();

            await using var command =
                new MySqlCommand(sql, connection);

            await command.ExecuteNonQueryAsync();

            logger.LogInformation(
                "[MySQL] Table '{TableName}' created/verified successfully.",
                tableName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[MySQL] Failed to create table '{TableName}'.",
                tableName);

            throw;
        }
    }

    private static string GetSqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int32 => "INT",
            TypeCode.Int64 => "BIGINT",
            TypeCode.Int16 => "SMALLINT",
            TypeCode.Byte => "TINYINT",
            TypeCode.Boolean => "BOOLEAN",
            TypeCode.Decimal => "DECIMAL",
            TypeCode.Double => "DOUBLE",
            TypeCode.Single => "FLOAT",
            TypeCode.DateTime => "DATETIME",
            TypeCode.String => "TEXT",

            _ when type == typeof(Guid)
                => "CHAR(36)",

            _ when type == typeof(byte[])
                => "BLOB",

            _ => "TEXT"
        };
    }
}