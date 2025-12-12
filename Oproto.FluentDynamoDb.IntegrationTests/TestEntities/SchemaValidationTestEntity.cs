using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with GSI, LSI, and TTL for schema validation integration testing.
/// This entity has a comprehensive configuration to test all validation scenarios.
/// </summary>
[DynamoDbTable("test-schema-validation")]
public partial class SchemaValidationTestEntity
{
    /// <summary>
    /// Partition key for the base table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    /// <summary>
    /// Sort key for the base table.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string ItemId { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI partition key - allows querying by status across all tenants.
    /// </summary>
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
    
    /// <summary>
    /// GSI sort key - used with StatusIndex.
    /// </summary>
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_at")]
    public string? CreatedAt { get; set; }
    
    /// <summary>
    /// LSI sort key - allows querying items by category within a tenant.
    /// </summary>
    [LocalSecondaryIndex("CategoryIndex")]
    [DynamoDbAttribute("category")]
    public string? Category { get; set; }
    
    /// <summary>
    /// TTL attribute - items will be automatically deleted after this timestamp.
    /// </summary>
    [TimeToLive]
    [DynamoDbAttribute("ttl")]
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Regular attribute for testing.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    /// <summary>
    /// Regular attribute for testing.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Simple test entity with only primary key for testing basic schema validation.
/// </summary>
[DynamoDbTable("test-simple-validation")]
public partial class SimpleSchemaValidationEntity
{
    /// <summary>
    /// Partition key only - no sort key.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Regular attribute.
    /// </summary>
    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}

/// <summary>
/// Test entity with only GSI (no LSI or TTL) for targeted GSI validation testing.
/// </summary>
[DynamoDbTable("test-gsi-only-validation")]
public partial class GsiOnlyValidationEntity
{
    /// <summary>
    /// Partition key for the base table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Sort key for the base table.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI partition key - allows querying by email.
    /// </summary>
    [GlobalSecondaryIndex("EmailIndex")]
    [DynamoDbAttribute("email")]
    public string? Email { get; set; }
    
    /// <summary>
    /// Regular attribute.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
}
