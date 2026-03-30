using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Test entity for typed Map operations in DynamicFieldCollection.
/// Used to test GetMap, TryGetMap, SetMap, and prefix-based typed Map retrieval.
/// </summary>
/// <remarks>
/// This entity is decorated with [DynamoDbEntity] to have the source generator
/// create the IReadOnlyEntity and IDynamoDbEntity interface implementations,
/// enabling it to be used with the typed Map operations.
/// </remarks>
[DynamoDbEntity]
public partial class TestNestedEntity
{
    /// <summary>
    /// A decimal amount for testing numeric serialization.
    /// </summary>
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// A string name for testing string serialization.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// An integer count for testing integer serialization.
    /// </summary>
    [DynamoDbAttribute("count")]
    public int Count { get; set; }

    /// <summary>
    /// A boolean flag for testing boolean serialization.
    /// </summary>
    [DynamoDbAttribute("isActive")]
    public bool IsActive { get; set; }
}
