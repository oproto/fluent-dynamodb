using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a property model extracted from source analysis.
/// </summary>
internal class PropertyModel
{
    /// <summary>
    /// Gets or sets the property name in C#.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property type as a string.
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this property is the partition key.
    /// </summary>
    public bool IsPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is the sort key.
    /// </summary>
    public bool IsSortKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is a collection type.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets the key format information for partition/sort keys.
    /// </summary>
    public KeyFormatModel? KeyFormat { get; set; }

    /// <summary>
    /// Gets or sets the queryable information for this property.
    /// </summary>
    public QueryableModel? Queryable { get; set; }

    /// <summary>
    /// Gets or sets the Global Secondary Index attributes for this property.
    /// </summary>
    public GlobalSecondaryIndexModel[] GlobalSecondaryIndexes { get; set; } = Array.Empty<GlobalSecondaryIndexModel>();

    /// <summary>
    /// Gets or sets the computed key information for this property.
    /// </summary>
    public ComputedKeyModel? ComputedKey { get; set; }

    /// <summary>
    /// Gets or sets the extracted key information for this property.
    /// </summary>
    public ExtractedKeyModel? ExtractedKey { get; set; }

    /// <summary>
    /// Gets or sets the original property declaration syntax node.
    /// </summary>
    public PropertyDeclarationSyntax? PropertyDeclaration { get; set; }

    /// <summary>
    /// Gets a value indicating whether this property has DynamoDB attribute mapping.
    /// </summary>
    public bool HasAttributeMapping => !string.IsNullOrEmpty(AttributeName);

    /// <summary>
    /// Gets a value indicating whether this property is part of any GSI.
    /// </summary>
    public bool IsPartOfGsi => GlobalSecondaryIndexes.Length > 0;

    /// <summary>
    /// Gets a value indicating whether this property is computed from other properties.
    /// </summary>
    public bool IsComputed => ComputedKey != null;

    /// <summary>
    /// Gets a value indicating whether this property is extracted from another property.
    /// </summary>
    public bool IsExtracted => ExtractedKey != null;

    /// <summary>
    /// Gets a value indicating whether this property is read-only (computed or extracted).
    /// </summary>
    public bool IsReadOnly => IsComputed || IsExtracted;

    /// <summary>
    /// Gets or sets the advanced type information for this property.
    /// </summary>
    public AdvancedTypeInfo? AdvancedType { get; set; }

    /// <summary>
    /// Gets or sets the security information for this property.
    /// </summary>
    public SecurityInfo? Security { get; set; }

    /// <summary>
    /// Gets or sets the format string from DynamoDbAttribute for value serialization.
    /// </summary>
    public string? Format { get; set; }
}