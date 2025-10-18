namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as part of a Global Secondary Index (GSI).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class GlobalSecondaryIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the Global Secondary Index.
    /// </summary>
    public string IndexName { get; }

    /// <summary>
    /// Gets or sets whether this property is the partition key for the GSI.
    /// </summary>
    public bool IsPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property is the sort key for the GSI.
    /// </summary>
    public bool IsSortKey { get; set; }

    /// <summary>
    /// Gets or sets the key format pattern for composite keys.
    /// </summary>
    public string? KeyFormat { get; set; }

    /// <summary>
    /// Initializes a new instance of the GlobalSecondaryIndexAttribute class.
    /// </summary>
    /// <param name="indexName">The name of the Global Secondary Index.</param>
    public GlobalSecondaryIndexAttribute(string indexName)
    {
        IndexName = indexName;
    }
}