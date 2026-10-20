using DataAccess.Abstractions.Attributes;
using DataAccess.Core.Metadata;
using Xunit;

namespace DataAccess.Core.Tests;

public sealed class EntityMetadataTests
{
    [Fact]
    public void Resolves_Attributed_Properties_And_Values()
    {
        var entity = new Entity { Key = 42, Email = "test@example.com", Name = "Test" };
        Assert.Equal(nameof(Entity.Key), EntityMetadata.GetPrimaryKey<Entity>().Name);
        Assert.Equal(42, EntityMetadata.GetPrimaryKeyValue(entity));
        Assert.Equal(nameof(Entity.Email), EntityMetadata.GetUnique<Entity>()!.Name);
        Assert.Equal(entity.Email, EntityMetadata.GetUniqueValue(entity));
        Assert.Equal(nameof(Entity.Name), EntityMetadata.GetIndexed<Entity>()!.Name);
        Assert.Equal(entity.Name, EntityMetadata.GetIndexedValue(entity));
    }

    [Fact]
    public void Missing_Primary_Key_Reports_Entity_Name()
    {
        var error = Assert.Throws<InvalidOperationException>(() => EntityMetadata.GetPrimaryKey<Unmapped>());
        Assert.Contains(nameof(Unmapped), error.Message);
        Assert.Contains("[PrimaryKey]", error.Message);
    }

    [Fact]
    public void Missing_Optional_Attributes_Return_Null()
    {
        Assert.Null(EntityMetadata.GetUnique<Unmapped>());
        Assert.Null(EntityMetadata.GetIndexed<Unmapped>());
        Assert.Null(EntityMetadata.GetUniqueValue(new Unmapped()));
        Assert.Null(EntityMetadata.GetIndexedValue(new Unmapped()));
    }

    [Fact]
    public void Multiple_Primary_Keys_Are_Rejected() =>
        Assert.Throws<InvalidOperationException>(() => EntityMetadata.GetPrimaryKey<MultipleKeys>());

    [Fact]
    public void Table_Name_Defaults_To_Entity_Name_And_Honors_Schema_Attribute()
    {
        Assert.Equal(nameof(Unmapped), EntityMetadata.GetTableName<Unmapped>());
        Assert.Equal("custom_table", EntityMetadata.GetTableName<Mapped>());
    }

    public sealed class Entity
    {
        [PrimaryKey] public int Key { get; set; }
        [Unique] public string? Email { get; set; }
        [Indexed] public string? Name { get; set; }
    }
    public sealed class Unmapped { }
    public sealed class MultipleKeys
    {
        [PrimaryKey] public int First { get; set; }
        [PrimaryKey] public int Second { get; set; }
    }
    [System.ComponentModel.DataAnnotations.Schema.Table("custom_table")]
    public sealed class Mapped { }
}
