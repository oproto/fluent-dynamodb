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

    /// <summary>
    /// Initializes a new instance of the DynamoDbAttributeAttribute class.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    public DynamoDbAttributeAttribute(string attributeName)
    {
        AttributeName = attributeName;
    }
}
