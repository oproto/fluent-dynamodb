namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with computed partition key (Year, Month, Day source properties) and a simple sort key.
/// Used by API surface compile tests to verify typed parameter convenience overloads are generated
/// for Get, Delete, Update, and ConditionCheck.
/// Requirements: 11.5, 1.1, 1.4, 1.6
/// </summary>
[DynamoDbTable("computedKeyTable")]
public partial class ComputedKeyEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
}
