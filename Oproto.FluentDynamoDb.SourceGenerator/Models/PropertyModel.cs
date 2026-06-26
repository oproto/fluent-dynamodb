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
    /// Gets or sets a value indicating whether this property's type is an enum.
    /// Set from Roslyn semantic analysis (<c>ITypeSymbol.TypeKind == TypeKind.Enum</c>) during entity analysis,
    /// providing reliable enum detection without name-based heuristics.
    /// </summary>
    public bool IsEnum { get; set; }

    /// <summary>
    /// Gets or sets the key format information for partition/sort keys.
    /// </summary>
    public KeyFormatModel? KeyFormat { get; set; }

    /// <summary>
    /// Gets or sets the GSI partition key attributes for this property (new attribute model).
    /// </summary>
    public GsiPartitionKeyModel[] GsiPartitionKeys { get; set; } = Array.Empty<GsiPartitionKeyModel>();

    /// <summary>
    /// Gets or sets the GSI sort key attributes for this property (new attribute model).
    /// </summary>
    public GsiSortKeyModel[] GsiSortKeys { get; set; } = Array.Empty<GsiSortKeyModel>();

    /// <summary>
    /// Gets or sets the LSI sort key attributes for this property (new attribute model).
    /// </summary>
    public LsiSortKeyModel[] LsiSortKeys { get; set; } = Array.Empty<LsiSortKeyModel>();

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
    public bool IsPartOfGsi => GsiPartitionKeys.Length > 0 || GsiSortKeys.Length > 0;

    /// <summary>
    /// Gets a value indicating whether this property is part of any LSI.
    /// </summary>
    public bool IsPartOfLsi => LsiSortKeys.Length > 0;

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
    /// Gets or sets the complex type information for this property.
    /// </summary>
    public ComplexTypeInfo? ComplexType { get; set; }

    /// <summary>
    /// Gets or sets the security information for this property.
    /// </summary>
    public SecurityInfo? Security { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property has [RelatedEntity] attribute.
    /// Used to suppress DYNDB023 performance warnings for intentional composite entity patterns.
    /// </summary>
    public bool IsRelatedEntity { get; set; }

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

    /// <summary>
    /// Gets or sets the normalized key format string for this key property.
    /// Populated by EntityAnalyzer for partition keys and sort keys.
    /// For computed keys: uses the format computed by ComputeFormatString.
    /// For non-computed keys with prefix: "{Prefix}{Separator}{0}".
    /// For non-computed keys without prefix: "{0}".
    /// Null for non-key properties.
    /// </summary>
    public string? NormalizedKeyFormat { get; set; }

    /// <summary>
    /// Gets or sets the discriminator pattern derived from NormalizedKeyFormat.
    /// Computed by replacing each {N} placeholder with *.
    /// Null when NormalizedKeyFormat is "{0}" (no discrimination capability)
    /// or when the property is not a key property.
    /// </summary>
    public string? DerivedDiscriminatorPattern { get; set; }
}