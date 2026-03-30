using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Nested type representing an address for testing nested property access.
/// </summary>
[DynamoDbEntity]
public partial class TestAddress
{
    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;

    [DynamoDbAttribute("state")]
    public string State { get; set; } = string.Empty;

    [DynamoDbAttribute("zipCode")]
    public string ZipCode { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("country")]
    public TestCountry Country { get; set; } = new();
}

/// <summary>
/// Nested type representing a country for multi-level nesting tests.
/// </summary>
[DynamoDbEntity]
public partial class TestCountry
{
    [DynamoDbAttribute("code")]
    public string Code { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Nested type representing metadata with lists for testing nested list access.
/// </summary>
[DynamoDbEntity]
public partial class TestMetadata
{
    [DynamoDbAttribute("keywords")]
    public List<string> Keywords { get; set; } = new();

    [DynamoDbAttribute("scores")]
    public List<int> Scores { get; set; } = new();
}

/// <summary>
/// Nested type representing a line item for testing object properties in lists.
/// </summary>
[DynamoDbEntity]
public partial class TestLineItem
{
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;

    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}

/// <summary>
/// Test entity with nested properties for integration testing of nested filter expressions.
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4
/// </summary>
[DynamoDbTable("nested-property-test", IsDefault = true)]
[GenerateEntityProperty(Name = "Entities")]
[Scannable]
public partial class NestedPropertyTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Type { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Nested address for single-level and multi-level nesting tests.
    /// </summary>
    [DynamoDbMap]
    [DynamoDbAttribute("address")]
    public TestAddress Address { get; set; } = new();

    /// <summary>
    /// Nested metadata with lists for nested list access tests.
    /// </summary>
    [DynamoDbMap]
    [DynamoDbAttribute("metadata")]
    public TestMetadata Metadata { get; set; } = new();

    /// <summary>
    /// List of strings for basic list index access tests.
    /// </summary>
    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
}
