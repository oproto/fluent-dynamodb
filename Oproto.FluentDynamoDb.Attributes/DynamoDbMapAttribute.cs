namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property for explicit conversion to a DynamoDB Map (M) type.
/// Without this attribute, Dictionary&lt;string, string&gt; uses default conversion.
/// With this attribute, custom objects are recursively mapped to nested DynamoDB Maps.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbMapAttribute : Attribute
{
    // Explicit marker for complex object -> Map conversion
}
