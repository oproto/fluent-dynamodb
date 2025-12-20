namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Test entity with [UseFluentResults] attribute for validating table-level
/// Result-returning convenience methods are generated correctly.
/// Requirements: 3.1, 3.2
/// </summary>
[DynamoDbTable("fluentResultsTable")]
[UseFluentResults]
public partial class FluentResultsEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("count")]
    public int Count { get; set; }
}

/// <summary>
/// Test entity with [UseFluentResults(HideGeneratedAsyncMethods = false)] for validating
/// that both traditional async methods and Result-returning methods are generated.
/// Requirements: 3.4
/// </summary>
[DynamoDbTable("fluentResultsBothTable")]
[UseFluentResults(HideGeneratedAsyncMethods = false)]
public partial class FluentResultsBothEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("value")]
    public string Value { get; set; } = string.Empty;
}
