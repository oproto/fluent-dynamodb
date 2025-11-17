using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Maps a property to a DynamoDB attribute with a specific name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbAttributeAttribute : Attribute
{
    /// <summary>
    /// Gets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; }

    /// <summary>
    /// Gets or sets the format string to apply when serializing this property's value in LINQ expressions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The format string is applied during LINQ expression translation to ensure consistent formatting
    /// of values sent to DynamoDB. This is particularly useful for DateTime, decimal, and other numeric types.
    /// </para>
    /// <para>
    /// Common format examples:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>DateTime: "yyyy-MM-dd" (e.g., "2024-10-24"), "yyyy-MM-ddTHH:mm:ss" (ISO 8601)</description>
    /// </item>
    /// <item>
    /// <description>Decimal/Double: "F2" (two decimal places, e.g., "123.45"), "N2" (with thousand separators)</description>
    /// </item>
    /// <item>
    /// <description>Integer: "D5" (zero-padded to 5 digits, e.g., "00123")</description>
    /// </item>
    /// <item>
    /// <description>Currency: "C" (currency format based on culture)</description>
    /// </item>
    /// </list>
    /// <para>
    /// If not specified, default serialization is used. All formatting uses InvariantCulture for consistency.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Transaction
    /// {
    ///     [DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]
    ///     public DateTime CreatedDate { get; set; }
    ///     
    ///     [DynamoDbAttribute("amount", Format = "F2")]
    ///     public decimal Amount { get; set; }
    ///     
    ///     [DynamoDbAttribute("order_id", Format = "D8")]
    ///     public int OrderId { get; set; }
    /// }
    /// </code>
    /// </example>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the DateTimeKind to apply when serializing and deserializing DateTime properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property controls timezone handling for DateTime values during serialization and deserialization.
    /// When specified, the source generator will ensure DateTime values are converted to the specified kind
    /// before storage and have their Kind property set correctly after retrieval.
    /// </para>
    /// <para>
    /// <strong>DateTimeKind Options:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description><strong>Unspecified</strong> (default): No timezone conversion is performed. The DateTime is stored and retrieved as-is.</description>
    /// </item>
    /// <item>
    /// <description><strong>Utc</strong>: DateTime values are converted to UTC before serialization using ToUniversalTime(). 
    /// After deserialization, the Kind property is set to DateTimeKind.Utc. Recommended for most scenarios to ensure consistent timezone handling.</description>
    /// </item>
    /// <item>
    /// <description><strong>Local</strong>: DateTime values are converted to local time before serialization using ToLocalTime(). 
    /// After deserialization, the Kind property is set to DateTimeKind.Local. Use with caution as local time depends on server timezone.</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Best Practices:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Use <strong>DateTimeKind.Utc</strong> for timestamps, audit trails, and any time-sensitive data that needs to be consistent across timezones.</description>
    /// </item>
    /// <item>
    /// <description>Use <strong>DateTimeKind.Local</strong> only when you specifically need local time representation and understand the implications.</description>
    /// </item>
    /// <item>
    /// <description>Use <strong>DateTimeKind.Unspecified</strong> when timezone information is not relevant or when you're managing timezone conversion manually.</description>
    /// </item>
    /// </list>
    /// <para>
    /// This property can be combined with the Format property to control both timezone handling and string representation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Event
    /// {
    ///     // Store as UTC timestamp with ISO 8601 format
    ///     [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc, Format = "o")]
    ///     public DateTime CreatedAt { get; set; }
    ///     
    ///     // Store as UTC date-only (no time component)
    ///     [DynamoDbAttribute("event_date", DateTimeKind = DateTimeKind.Utc, Format = "yyyy-MM-dd")]
    ///     public DateTime EventDate { get; set; }
    ///     
    ///     // Store without timezone conversion
    ///     [DynamoDbAttribute("scheduled_time", DateTimeKind = DateTimeKind.Unspecified)]
    ///     public DateTime ScheduledTime { get; set; }
    /// }
    /// </code>
    /// </example>
    public DateTimeKind DateTimeKind { get; set; } = DateTimeKind.Unspecified;

    private int _geoHashPrecision = 0;

    /// <summary>
    /// Gets or sets the GeoHash precision level for GeoLocation properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property controls the precision of GeoHash encoding when serializing GeoLocation properties
    /// from the Oproto.FluentDynamoDb.Geospatial package. The precision determines the size of the geographic
    /// area represented by each GeoHash cell, affecting both query accuracy and efficiency.
    /// </para>
    /// <para>
    /// <strong>Precision Levels and Accuracy:</strong>
    /// </para>
    /// <list type="table">
    /// <listheader>
    /// <term>Precision</term>
    /// <description>Cell Size</description>
    /// <description>Use Case</description>
    /// </listheader>
    /// <item>
    /// <term>1</term>
    /// <description>±2500 km</description>
    /// <description>Continental queries</description>
    /// </item>
    /// <item>
    /// <term>2</term>
    /// <description>±630 km</description>
    /// <description>Country-level queries</description>
    /// </item>
    /// <item>
    /// <term>3</term>
    /// <description>±78 km</description>
    /// <description>Large city queries</description>
    /// </item>
    /// <item>
    /// <term>4</term>
    /// <description>±20 km</description>
    /// <description>City queries</description>
    /// </item>
    /// <item>
    /// <term>5</term>
    /// <description>±2.4 km</description>
    /// <description>Neighborhood queries</description>
    /// </item>
    /// <item>
    /// <term>6</term>
    /// <description>±0.61 km</description>
    /// <description>District queries (default)</description>
    /// </item>
    /// <item>
    /// <term>7</term>
    /// <description>±0.076 km</description>
    /// <description>Street-level queries</description>
    /// </item>
    /// <item>
    /// <term>8</term>
    /// <description>±0.019 km</description>
    /// <description>Building-level queries</description>
    /// </item>
    /// <item>
    /// <term>9</term>
    /// <description>±4.8 m</description>
    /// <description>Precise location queries</description>
    /// </item>
    /// <item>
    /// <term>10</term>
    /// <description>±1.2 m</description>
    /// <description>Very precise queries</description>
    /// </item>
    /// <item>
    /// <term>11</term>
    /// <description>±0.149 m</description>
    /// <description>Sub-meter precision</description>
    /// </item>
    /// <item>
    /// <term>12</term>
    /// <description>±0.037 m</description>
    /// <description>Centimeter precision</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Default Value:</strong> If not specified (value is 0), the source generator uses a default precision of 6,
    /// which provides approximately 610-meter accuracy and is suitable for most location-based queries.
    /// </para>
    /// <para>
    /// <strong>Trade-offs:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description><strong>Lower precision (1-5)</strong>: Larger geographic areas, fewer queries needed, less accurate results</description>
    /// </item>
    /// <item>
    /// <description><strong>Medium precision (6-7)</strong>: Balanced accuracy and query efficiency, recommended for most applications</description>
    /// </item>
    /// <item>
    /// <description><strong>Higher precision (8-12)</strong>: Smaller geographic areas, more queries may be needed for large areas, very accurate results</description>
    /// </item>
    /// </list>
    /// <para>
    /// This property only applies to properties of type GeoLocation from the Oproto.FluentDynamoDb.Geospatial package.
    /// If the geospatial package is not referenced, this property is ignored.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Store
    /// {
    ///     [PartitionKey]
    ///     [DynamoDbAttribute("pk")]
    ///     public string StoreId { get; set; }
    ///     
    ///     // Use precision 7 for street-level accuracy
    ///     [DynamoDbAttribute("location", GeoHashPrecision = 7)]
    ///     public GeoLocation Location { get; set; }
    ///     
    ///     // Use default precision 6 for district-level accuracy
    ///     [DynamoDbAttribute("delivery_area")]
    ///     public GeoLocation DeliveryArea { get; set; }
    /// }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is set to a number outside the valid range of 0-12 (where 0 means use default).
    /// </exception>
    public int GeoHashPrecision
    {
        get => _geoHashPrecision;
        set
        {
            if (value < 0 || value > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(GeoHashPrecision),
                    value,
                    "GeoHash precision must be between 0 (default) and 12.");
            }
            _geoHashPrecision = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbAttributeAttribute class.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    public DynamoDbAttributeAttribute(string attributeName)
    {
        AttributeName = attributeName;
    }
}
