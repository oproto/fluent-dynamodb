using System;

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
    /// Gets or sets the GSI-specific discriminator property name.
    /// Overrides the table-level discriminator for queries on this GSI.
    /// </summary>
    /// <remarks>
    /// Use this when the GSI uses a different discriminator strategy than the primary key.
    /// For example, the primary key might use "SK" with pattern "USER#*", while the GSI
    /// uses "GSI1SK" with pattern "USER#*".
    /// </remarks>
    /// <example>
    /// <code>
    /// [GlobalSecondaryIndex("StatusIndex",
    ///     DiscriminatorProperty = "GSI1SK",
    ///     DiscriminatorPattern = "USER#*")]
    /// </code>
    /// </example>
    public string? DiscriminatorProperty { get; set; }

    /// <summary>
    /// Gets or sets the GSI-specific discriminator value.
    /// Mutually exclusive with <see cref="DiscriminatorPattern"/>.
    /// </summary>
    public string? DiscriminatorValue { get; set; }

    /// <summary>
    /// Gets or sets the GSI-specific discriminator pattern (supports * wildcard).
    /// Mutually exclusive with <see cref="DiscriminatorValue"/>.
    /// </summary>
    public string? DiscriminatorPattern { get; set; }

    /// <summary>
    /// Initializes a new instance of the GlobalSecondaryIndexAttribute class.
    /// </summary>
    /// <param name="indexName">The name of the Global Secondary Index.</param>
    public GlobalSecondaryIndexAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
