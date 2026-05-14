using System;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the sort key for a Global Secondary Index (GSI).
/// The key role is encoded in the attribute name, eliminating the need for boolean flags.
/// </summary>
/// <remarks>
/// Discriminator configuration is an index-level concern that belongs on the
/// <see cref="GsiPartitionKeyAttribute"/> declaration. If only a <c>[GsiSortKey]</c> specifies
/// <see cref="Name"/> or <see cref="ProjectionType"/>, those values are used as fallbacks when
/// the <c>[GsiPartitionKey]</c> for the same index doesn't specify them.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class GsiSortKeyAttribute : Attribute
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
    /// [GsiSortKey("status-index", Name = "StatusIndex")]
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
    /// [GsiSortKey("status-index", ProjectionType = ProjectionType.KeysOnly)]
    /// [DynamoDbAttribute("createdAt")]
    /// public DateTime CreatedAt { get; set; }
    /// </code>
    /// </example>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;

    /// <summary>
    /// Initializes a new instance of the <see cref="GsiSortKeyAttribute"/> class.
    /// </summary>
    /// <param name="indexName">The name of the Global Secondary Index.</param>
    public GsiSortKeyAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
