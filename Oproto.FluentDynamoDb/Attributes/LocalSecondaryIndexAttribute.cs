using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the sort key for a Local Secondary Index (LSI).
/// LSIs share the same partition key as the base table.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LocalSecondaryIndexAttribute : Attribute
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
    /// [LocalSecondaryIndex("lsi1", Name = "CreatedAtIndex")]
    /// // Generates: table.CreatedAtIndex.Query&lt;T&gt;()
    /// </code>
    /// </example>
    public string? Name { get; set; }

    /// <summary>
    /// Initializes a new instance of the LocalSecondaryIndexAttribute class.
    /// </summary>
    /// <param name="indexName">The name of the Local Secondary Index.</param>
    public LocalSecondaryIndexAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
