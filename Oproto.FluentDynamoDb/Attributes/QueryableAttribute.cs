using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Defines the supported DynamoDB operations for a property.
/// Used in generated PropertyMetadata to describe what operations each property supports
/// based on its key role (partition key, sort key, or non-key).
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
