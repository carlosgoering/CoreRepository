using DataAccess.Abstractions.Attributes;
using Google.Cloud.Firestore;
namespace DataAccess.Firestore.Tests.Entities;
[FirestoreData]
public sealed class TestEntity
{
    [PrimaryKey] [FirestoreProperty] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [FirestoreProperty] public string Name { get; set; } = string.Empty;
    [FirestoreProperty] public int Age { get; set; }
    [FirestoreProperty] public int? OptionalAge { get; set; }
    [FirestoreProperty] public DateTime CreatedAt { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
