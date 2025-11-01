namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as containing sensitive data that should be excluded from logging output.
/// When applied, the property value will be replaced with "[REDACTED]" in all log messages
/// to ensure compliance with data protection regulations.
/// This attribute does not affect how the data is stored in DynamoDB.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SensitiveAttribute : Attribute
{
    // Marker attribute - no properties needed
}
