namespace DataAccess.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IndexedAttribute : Attribute
{
}