using DataAccess.Core.Metadata;
using Microsoft.Extensions.Logging;
using SQLite;

namespace DataAccess.SQLite.ClassMap;

internal static class ClassMapRegistration
{
    public static async Task Register<TEntity>(
        SQLiteAsyncConnection database,
        ILogger logger)
        where TEntity : class, new()
    {
        var type = typeof(TEntity);
        var tableName = type.Name;

        logger.LogInformation(
            $"[SQLite] Registering table '{tableName}'.");

        logger.LogDebug(
            $"[SQLite] Database path: {database.DatabasePath}");

        var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();

        if (primaryKey is null)
        {
            var exception = new InvalidOperationException(
                $"Entity '{type.Name}' must define a [PrimaryKey].");

            logger.LogError(
                exception,
                $"[SQLite] Failed to register table '{tableName}'.");

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
            $"[SQLite] SQL for '{tableName}':{Environment.NewLine}{sql}");

        try
        {
            await database.ExecuteAsync(sql);

            logger.LogInformation(
                $"[SQLite] Table '{tableName}' created/verified successfully.");

            var tables = await database.QueryAsync<TableInfo>(
                "SELECT name FROM sqlite_master WHERE type = 'table';");

            logger.LogDebug(
                $"[SQLite] Tables currently present in database '{database.DatabasePath}':");

            foreach (var table in tables)
            {
                logger.LogDebug(
                    $"[SQLite] Table: {table.Name}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                $"[SQLite] Failed to create table '{tableName}'.");

            throw;
        }
    }

    private sealed class TableInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private static string GetSqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int32 => "INTEGER",
            TypeCode.Int64 => "INTEGER",
            TypeCode.Int16 => "INTEGER",
            TypeCode.Byte => "INTEGER",
            TypeCode.Boolean => "INTEGER",
            TypeCode.Decimal => "REAL",
            TypeCode.Double => "REAL",
            TypeCode.Single => "REAL",
            TypeCode.DateTime => "TEXT",
            TypeCode.String => "TEXT",
            _ => "TEXT"
        };
    }
}