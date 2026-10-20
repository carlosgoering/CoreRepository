using DataAccess.Abstractions.Attributes;

namespace DataAccess.MongoDB.Tests;

public enum TestStatus : byte
{
    Pending = 0,
    Active = 1,
    Disabled = 2
}

public class TestEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Unique]
    public string Email { get; set; } = string.Empty;

    [Indexed]
    public string Category { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public TestStatus Status { get; set; }
}