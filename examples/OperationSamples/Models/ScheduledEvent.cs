using Oproto.FluentDynamoDb.Attributes;

namespace FluentDynamoDb.OperationSamples.Models;

/// <summary>
/// Represents a scheduled event with a computed partition key composed from Year, Month, and Day.
/// 
/// This entity demonstrates the <c>[Computed]</c> and <c>[Extracted]</c> attributes that enable
/// typed convenience overloads. The source generator produces Get, Delete, and Update methods
/// that accept individual typed parameters (int year, int month, int day) instead of requiring
/// manual key string composition.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "{Year}#{Month}#{Day}" - computed from source properties</description></item>
/// <item><description>Sort Key (sk): Event identifier</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("Orders")]
[GenerateEntityProperty(Name = "ScheduledEvents")]
public partial class ScheduledEvent
{
    /// <summary>
    /// Gets or sets the partition key, computed from Year, Month, and Day joined by "#".
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key identifying the specific event.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the year component extracted from the partition key.
    /// </summary>
    [Extracted("Pk", 0)]
    [DynamoDbAttribute("year")]
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the month component extracted from the partition key.
    /// </summary>
    [Extracted("Pk", 1)]
    [DynamoDbAttribute("month")]
    public int Month { get; set; }

    /// <summary>
    /// Gets or sets the day component extracted from the partition key.
    /// </summary>
    [Extracted("Pk", 2)]
    [DynamoDbAttribute("day")]
    public int Day { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled event.
    /// </summary>
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
}
