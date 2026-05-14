using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with various HashSet properties for set operation integration testing.
/// Used to test AddToSet and DeleteFromSet operations.
/// 
/// Requirements covered:
/// - 5.1: Support Add operation for sets (single element)
/// - 5.2: Support adding multiple elements to set
/// - 5.3: Support Delete operation for sets (single element)
/// - 5.4: Support deleting multiple elements from set
/// - 5.5: Set operations work with numeric sets
/// </summary>
[DynamoDbTable("set-operation-test", IsDefault = true)]
[GenerateEntityProperty(Name = "Entities")]
public partial class SetOperationTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// String set for testing string set operations.
    /// </summary>
    [DynamoDbAttribute("categories")]
    public HashSet<string>? Categories { get; set; }

    /// <summary>
    /// Integer set for testing numeric set operations.
    /// </summary>
    [DynamoDbAttribute("scores")]
    public HashSet<int>? Scores { get; set; }

    /// <summary>
    /// Long set for testing large numeric set operations.
    /// </summary>
    [DynamoDbAttribute("largeNumbers")]
    public HashSet<long>? LargeNumbers { get; set; }

    /// <summary>
    /// Decimal set for testing decimal numeric set operations.
    /// </summary>
    [DynamoDbAttribute("prices")]
    public HashSet<decimal>? Prices { get; set; }
}
