using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests with a separate sort key.
/// Represents a store with an S2-indexed geographic location stored as a regular attribute,
/// and a timestamp as the sort key. This allows testing spatial queries combined with
/// sort key conditions (e.g., find stores within radius that opened after a certain date).
/// </summary>
[DynamoDbTable("s2-stores-with-sortkey")]
[GenerateAccessors]
public partial class S2StoreWithSortKeyEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    /// <summary>
    /// Sort key representing when the store opened.
    /// Format: ISO 8601 timestamp (e.g., "2024-01-15T10:30:00Z")
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OpenedAt { get; set; } = string.Empty;
    
    /// <summary>
    /// Geographic location stored as S2 index (not the sort key).
    /// This allows combining spatial queries with sort key conditions.
    /// </summary>
    [DynamoDbAttribute("loc", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
    
    [DynamoDbAttribute("store_status")]
    public string Status { get; set; } = "OPEN";
}
