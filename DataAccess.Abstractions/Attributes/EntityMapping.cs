using System.Reflection;

namespace DataAccess.Abstractions.Attributes;

public sealed class EntityMapping
{
    public string TableName { get; init; } = string.Empty;
    public IReadOnlyList<PropertyInfo> PrimaryKeys { get; init; } = [];
    public IReadOnlyList<PropertyInfo> Indexed { get; init; } = [];
    public IReadOnlyList<PropertyInfo> Unique { get; init; } = [];
}