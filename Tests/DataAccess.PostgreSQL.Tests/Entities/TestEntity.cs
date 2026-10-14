using DataAccess.Abstractions.Attributes;

namespace DataAccess.PostgreSQL.Tests.Entities
{
    public enum TestStatus : byte
    {
        Pending,
        Active,
        Disabled
    }

    public sealed class TestEntity
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public int? OptionalAge { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public TestStatus Status { get; set; }

        public TestStatus? OptionalStatus { get; set; }
    }
}
