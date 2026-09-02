namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with computed partition key (Year, Month, Day) and [UseFluentResults] attribute.
/// HideGeneratedAsyncMethods defaults to true, so only Result-returning typed async methods
/// (GetAsyncResult, DeleteAsyncResult) are generated — standard GetAsync/DeleteAsync are suppressed.
/// Used by API surface tests to verify typed FluentResults convenience overloads for computed keys.
/// Requirements: 5.1, 6.1, 7.1
/// </summary>
[DynamoDbTable("computedKeyFluentResultsTable")]
[UseFluentResults]
public partial class ComputedKeyFluentResultsEntity
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

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Entity with computed partition key (Year, Month, Day) and [UseFluentResults(HideGeneratedAsyncMethods = false)].
/// Both standard typed async methods (GetAsync, DeleteAsync) AND Result-returning variants
/// (GetAsyncResult, DeleteAsyncResult) are generated for this entity.
/// Used by API surface tests to verify both method types coexist for computed key entities.
/// Requirements: 5.1, 6.1, 7.1
/// </summary>
[DynamoDbTable("computedKeyFluentResultsBothTable")]
[UseFluentResults(HideGeneratedAsyncMethods = false)]
public partial class ComputedKeyFluentResultsBothEntity
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

    [DynamoDbAttribute("label")]
    public string Label { get; set; } = string.Empty;
}
