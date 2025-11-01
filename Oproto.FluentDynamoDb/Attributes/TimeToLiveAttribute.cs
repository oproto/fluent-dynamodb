namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a DateTime or DateTimeOffset property as a DynamoDB Time-To-Live (TTL) field.
/// The value will be automatically converted to Unix epoch seconds when stored.
/// DynamoDB will automatically delete items when the TTL timestamp is reached.
/// Only one property per entity can be marked with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class TimeToLiveAttribute : Attribute
{
    // Marker attribute - no properties needed
    // Source generator validates only one per entity
}
