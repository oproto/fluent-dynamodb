namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Defines the supported DynamoDB operations for a property.
/// This mirrors the enum from the main library for source generator use.
/// </summary>
internal enum DynamoDbOperation
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
/// Represents queryable information for a property.
/// </summary>
internal class QueryableModel
{
    /// <summary>
    /// Gets or sets the supported DynamoDB operations for this property.
    /// </summary>
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();

    /// <summary>
    /// Gets or sets the indexes where this property is available for querying.
    /// </summary>
    public string[] AvailableInIndexes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether this property supports any operations.
    /// </summary>
    public bool HasSupportedOperations => SupportedOperations.Length > 0;

    /// <summary>
    /// Gets a value indicating whether this property is available in specific indexes.
    /// </summary>
    public bool HasIndexRestrictions => AvailableInIndexes.Length > 0;
}