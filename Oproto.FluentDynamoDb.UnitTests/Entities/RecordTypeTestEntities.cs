using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Test record entities for verifying record type support in the source generator.
/// These entities test various record declaration patterns.
/// </summary>

/// <summary>
/// Basic record type with [DynamoDbTable] attribute.
/// Tests that the source generator handles record declarations.
/// Note: Using get/set properties for compatibility with current generator.
/// _Requirements: 2.1_
/// </summary>
[DynamoDbTable("test-records")]
public partial record TestRecordEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("value")]
    public int Value { get; set; }

    [DynamoDbAttribute("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Record type with positional parameters (primary constructor).
/// Tests that the source generator handles positional record parameters.
/// Note: Positional records have init-only properties by default, so we use
/// a record with explicit get/set properties for compatibility.
/// _Requirements: 2.5_
/// </summary>
[DynamoDbTable("test-positional-records")]
public partial record TestPositionalRecordEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("count")]
    public int Count { get; set; }

    // Constructor for convenience
    public TestPositionalRecordEntity() { }
    
    public TestPositionalRecordEntity(string id, string sortKey, string name, int count)
    {
        Id = id;
        SortKey = sortKey;
        Name = name;
        Count = count;
    }
}

/// <summary>
/// Record type with mutable properties.
/// Tests that the source generator handles record property initialization.
/// Note: Using get/set properties for compatibility with current generator.
/// _Requirements: 2.4_
/// </summary>
[DynamoDbTable("test-init-records")]
public partial record TestInitOnlyRecordEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("description")]
    public string? Description { get; set; }

    [DynamoDbAttribute("is_active")]
    public bool IsActive { get; set; }

    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
}

// Note: Record structs are NOT supported because DynamoDbTableAttribute
// is restricted to class declarations. Record classes (record/record class)
// are supported because they compile to reference types.
