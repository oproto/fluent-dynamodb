using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Metadata about a property in a DynamoDB entity.
/// </summary>
public class PropertyMetadata
{
    /// <summary>
    /// Gets or sets the C# property name.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the C# property type.
    /// </summary>
    public Type PropertyType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets whether this property is the partition key.
    /// </summary>
    public bool IsPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property is the sort key.
    /// </summary>
    public bool IsSortKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property is a collection type.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Gets or sets whether this property is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets the DynamoDB operations supported by this property.
    /// </summary>
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();

    /// <summary>
    /// Gets or sets the indexes where this property is available for querying.
    /// </summary>
    public string[]? AvailableInIndexes { get; set; }

    /// <summary>
    /// Gets or sets key formatting information for partition and sort keys.
    /// </summary>
    public KeyFormatMetadata? KeyFormat { get; set; }

    /// <summary>
    /// Gets or sets the format string to apply when serializing this property's value in LINQ expressions.
    /// </summary>
    /// <remarks>
    /// This format string is applied during LINQ expression translation to ensure consistent formatting
    /// of values sent to DynamoDB. Common examples include "yyyy-MM-dd" for DateTime, "F2" for decimals.
    /// If null or empty, default serialization is used.
    /// </remarks>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets whether this property is encrypted at rest using field-level encryption.
    /// </summary>
    /// <remarks>
    /// When true, the property value is encrypted before being stored in DynamoDB and decrypted when retrieved.
    /// This requires an IFieldEncryptor to be configured in the DynamoDbOperationContext.
    /// Encrypted properties are marked with the [Encrypted] attribute in the entity class.
    /// </remarks>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Gets or sets the DateTimeKind for DateTime properties to control timezone handling during serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// When specified, DateTime values are converted to the specified kind before serialization
    /// and the Kind property is set after deserialization. This ensures consistent timezone handling across operations.
    /// Common values: DateTimeKind.Utc for UTC timestamps, DateTimeKind.Local for local time, DateTimeKind.Unspecified for no timezone conversion.
    /// If null, no timezone conversion is performed.
    /// </remarks>
    public DateTimeKind? DateTimeKind { get; set; }

    /// <summary>
    /// Gets or sets the GeoHash precision for GeoLocation properties.
    /// </summary>
    /// <remarks>
    /// When specified, GeoLocation values are encoded to GeoHash strings with the specified precision (1-12).
    /// This property is only applicable to GeoLocation properties when the Oproto.FluentDynamoDb.Geospatial package is referenced.
    /// If null, the default precision of 6 is used.
    /// </remarks>
    public int? GeoHashPrecision { get; set; }
}

/// <summary>
/// Metadata about key formatting for partition and sort keys.
/// </summary>
public class KeyFormatMetadata
{
    /// <summary>
    /// Gets or sets the prefix for the key value.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the separator used when combining key components.
    /// </summary>
    public string? Separator { get; set; }
}