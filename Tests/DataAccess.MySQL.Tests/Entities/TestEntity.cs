using DataAccess.Abstractions.Attributes;

namespace DataAccess.MySQL.Tests.Entities;
public sealed class TestEntity
{
    [PrimaryKey] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public int? OptionalAge { get; set; }
    public DateTime CreatedAt { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
