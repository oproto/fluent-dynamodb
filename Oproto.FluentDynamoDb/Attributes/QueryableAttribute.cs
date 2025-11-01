using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Defines the supported DynamoDB operations for a property.
/// </summary>
public enum DynamoDbOperation
{
    /// <summary>
    /// Equality comparison (=).
    /// </summary>
    Equals,

    /// <summary>
    /// Begins with comparison for strings.
    /// </summary>
    BeginsWith,

    /// <summary>
    /// Between comparison for ranges.
    /// </summary>
    Between,

    /// <summary>
    /// Greater than comparison (>).
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Less than comparison (<).
    /// </summary>
    LessThan,

    /// <summary>
    /// Contains comparison for sets and strings.
    /// </summary>
    Contains,

    /// <summary>
    /// In comparison for multiple values.
    /// </summary>
    In
}

/// <summary>
/// Marks a property as queryable and specifies the supported operations and indexes.
/// This metadata is used for future LINQ expression support.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class QueryableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the DynamoDB operations supported by this property.
    /// </summary>
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();

    /// <summary>
    /// Gets or sets the indexes where this property is available for querying.
    /// If null or empty, the property is available in the main table only.
    /// </summary>
    public string[]? AvailableInIndexes { get; set; }
}
