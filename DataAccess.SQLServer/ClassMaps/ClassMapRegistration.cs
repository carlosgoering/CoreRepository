using DataAccess.Core.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DataAccess.SqlServer.ClassMap;

internal static class ClassMapRegistration
{
    public static async Task Register<TEntity>(
        SqlConnection connection,
        ILogger logger)
        where TEntity : class, new()
    {
        var type = typeof(TEntity);
        var tableName = type.Name;

        logger.LogInformation(
            "[SQL Server] Registering table '{TableName}'.",
            tableName);

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        var columns = type
            .GetProperties()
            .Where(x => x.CanRead && x.CanWrite)
            .Select(x =>
            {
                var sqlType = GetSqlType(x.PropertyType);

                var definition =
                    $"[{x.Name}] {sqlType}";

                if (x == primaryKey)
                    definition += " PRIMARY KEY";

                return definition;
            })
            .ToArray();

        var sql = $"""
            IF OBJECT_ID(N'[{tableName}]', N'U') IS NULL
            BEGIN
                CREATE TABLE [{tableName}]
                (
                    {string.Join(",\n", columns)}
                );
            END
            """;

        logger.LogDebug(
            "[SQL Server] SQL for '{TableName}':{NewLine}{Sql}",
            tableName,
            Environment.NewLine,
            sql);

        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            await using var command =
                new SqlCommand(sql, connection);

            await command.ExecuteNonQueryAsync();

            logger.LogInformation(
                "[SQL Server] Table '{TableName}' created/verified successfully.",
                tableName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[SQL Server] Failed to create table '{TableName}'.",
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
            TypeCode.Boolean => "BIT",
            TypeCode.Decimal => "DECIMAL(18, 6)",
            TypeCode.Double => "FLOAT",
            TypeCode.Single => "REAL",
            TypeCode.DateTime => "DATETIME2",
            TypeCode.String => "NVARCHAR(MAX)",

            _ when type == typeof(Guid)
                => "UNIQUEIDENTIFIER",

            _ when type == typeof(DateTimeOffset)
                => "DATETIMEOFFSET",

            _ when type == typeof(byte[])
                => "VARBINARY(MAX)",

            _ => "NVARCHAR(MAX)"
        };
    }
}