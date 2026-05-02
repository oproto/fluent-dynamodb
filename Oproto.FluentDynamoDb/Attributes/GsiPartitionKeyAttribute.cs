using System;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the partition key for a Global Secondary Index (GSI).
/// The key role is encoded in the attribute name, eliminating the need for boolean flags.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class GsiPartitionKeyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the Global Secondary Index.
    /// </summary>
    public string IndexName { get; }

    /// <summary>
    /// Gets or sets the C# property name for the generated index accessor.
    /// If not specified, the name is derived from <see cref="IndexName"/> using PascalCase conversion.
    /// </summary>
    /// <example>
    /// <code>
    /// [GsiPartitionKey("status-index", Name = "StatusIndex")]
    /// // Generates: table.StatusIndex.Query&lt;T&gt;()
    /// </code>
    /// </example>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the DynamoDB projection type for this index.
    /// Defaults to <see cref="Metadata.ProjectionType.All"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is metadata only and reflects the actual DynamoDB index configuration.
    /// It does not affect query behavior - use <c>[UseProjection]</c> or <c>WithProjection()</c>
    /// to control what attributes are returned from queries.
    /// </para>
    /// <para>
    /// When set to <see cref="Metadata.ProjectionType.KeysOnly"/>, a read-only projection record
    /// is auto-generated containing the GSI keys and base table keys.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [GsiPartitionKey("status-index", ProjectionType = ProjectionType.KeysOnly)]
    /// [DynamoDbAttribute("status")]
    /// public string Status { get; set; }
    /// </code>
    /// </example>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;

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
    /// [GsiPartitionKey("StatusIndex",
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
    /// Initializes a new instance of the <see cref="GsiPartitionKeyAttribute"/> class.
    /// </summary>
    /// <param name="indexName">The name of the Global Secondary Index.</param>
    public GsiPartitionKeyAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
