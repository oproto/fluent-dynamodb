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
    /// Initializes a new instance of the DynamoDbAttributeAttribute class.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    public DynamoDbAttributeAttribute(string attributeName)
    {
        AttributeName = attributeName;
    }
}
