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

    /// <summary>
    /// Gets or sets the DateTimeKind for DateTime properties to control timezone handling during serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// When specified, the generated code will convert DateTime values to the specified kind before serialization
    /// and set the Kind property after deserialization. This ensures consistent timezone handling across operations.
    /// </remarks>
    public DateTimeKind? DateTimeKind { get; set; }

    /// <summary>
    /// Gets or sets the GeoHash precision for GeoLocation properties.
    /// Valid range is 1-12. If not specified, defaults to 6.
    /// </summary>
    public int? GeoHashPrecision { get; set; }

    /// <summary>
    /// Gets or sets the spatial index type for GeoLocation properties.
    /// Determines which spatial indexing algorithm to use (GeoHash, S2, or H3).
    /// </summary>
    public string? SpatialIndexType { get; set; }

    /// <summary>
    /// Gets or sets the S2 level for S2-indexed GeoLocation properties.
    /// Valid range is 0-30 (where 0 means use default 16).
    /// </summary>
    public int? S2Level { get; set; }

    /// <summary>
    /// Gets or sets the H3 resolution for H3-indexed GeoLocation properties.
    /// Valid range is 0-15 (where 0 means use default 9).
    /// </summary>
    public int? H3Resolution { get; set; }

    /// <summary>
    /// Gets or sets the latitude attribute name for coordinate storage.
    /// When set, the GeoLocation will be serialized with separate latitude and longitude attributes.
    /// </summary>
    public string? LatitudeAttributeName { get; set; }

    /// <summary>
    /// Gets or sets the longitude attribute name for coordinate storage.
    /// When set, the GeoLocation will be serialized with separate latitude and longitude attributes.
    /// </summary>
    public string? LongitudeAttributeName { get; set; }

    /// <summary>
    /// Gets a value indicating whether this property has coordinate storage configured.
    /// </summary>
    public bool HasCoordinateStorage => !string.IsNullOrEmpty(LatitudeAttributeName) && !string.IsNullOrEmpty(LongitudeAttributeName);
}