using System;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the sort key for a Local Secondary Index (LSI).
/// LSIs share the same partition key as the base table.
/// </summary>
/// <remarks>
/// No discriminator properties are needed on LSI attributes because LSIs share
/// the base table's partition key and don't need separate discrimination.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class LsiSortKeyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the Local Secondary Index.
    /// </summary>
    public string IndexName { get; }

    /// <summary>
    /// Gets or sets the C# property name for the generated index accessor.
    /// If not specified, the name is derived from <see cref="IndexName"/> using PascalCase conversion.
    /// </summary>
    /// <example>
    /// <code>
    /// [LsiSortKey("lsi1", Name = "CreatedAtIndex")]
    /// // Generates: table.CreatedAtIndex.Query&lt;T&gt;()
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
    /// is auto-generated containing the base table partition key, LSI sort key, and base table sort key.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [LsiSortKey("created-at-index", ProjectionType = ProjectionType.KeysOnly)]
    /// [DynamoDbAttribute("createdAt")]
    /// public DateTime CreatedAt { get; set; }
    /// </code>
    /// </example>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;

    /// <summary>
    /// Initializes a new instance of the <see cref="LsiSortKeyAttribute"/> class.
    /// </summary>
    /// <param name="indexName">The name of the Local Secondary Index.</param>
    public LsiSortKeyAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
