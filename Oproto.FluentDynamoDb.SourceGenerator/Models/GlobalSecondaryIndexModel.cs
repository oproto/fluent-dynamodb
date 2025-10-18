namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a Global Secondary Index attribute on a property.
/// </summary>
public class GlobalSecondaryIndexModel
{
    /// <summary>
    /// Gets or sets the name of the Global Secondary Index.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this property is the partition key for the GSI.
    /// </summary>
    public bool IsPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is the sort key for the GSI.
    /// </summary>
    public bool IsSortKey { get; set; }

    /// <summary>
    /// Gets or sets the key format pattern for composite keys.
    /// </summary>
    public string? KeyFormat { get; set; }

    /// <summary>
    /// Gets a value indicating whether this GSI attribute defines a key.
    /// </summary>
    public bool IsKey => IsPartitionKey || IsSortKey;

    /// <summary>
    /// Gets a value indicating whether this GSI has custom key formatting.
    /// </summary>
    public bool HasCustomKeyFormat => !string.IsNullOrEmpty(KeyFormat);
}