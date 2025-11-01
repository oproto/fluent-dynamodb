namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a class as a DynamoDB entity that should have mapping code generated.
/// Unlike [DynamoDbTable], this attribute is used for nested types that don't represent
/// a full table entity but need ToDynamoDb/FromDynamoDb methods for AOT-compatible mapping.
/// </summary>
/// <remarks>
/// Use this attribute on:
/// - Nested types used with [DynamoDbMap] attribute
/// - Custom types that need DynamoDB mapping but aren't top-level table entities
/// 
/// This ensures AOT compatibility by generating compile-time mapping methods
/// instead of relying on runtime reflection.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DynamoDbEntityAttribute : Attribute
{
}
