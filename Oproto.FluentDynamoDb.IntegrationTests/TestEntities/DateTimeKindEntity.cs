using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with DateTime properties configured with different DateTimeKind values
/// for integration testing DateTime Kind preservation.
/// </summary>
[DynamoDbTable("test-datetime-kind-entity")]
public partial class DateTimeKindEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    // DateTime with UTC kind
    [DynamoDbAttribute("utc_timestamp", DateTimeKind = DateTimeKind.Utc)]
    public DateTime? UtcTimestamp { get; set; }
    
    // DateTime with Local kind
    [DynamoDbAttribute("local_timestamp", DateTimeKind = DateTimeKind.Local)]
    public DateTime? LocalTimestamp { get; set; }
    
    // DateTime with Unspecified kind (default)
    [DynamoDbAttribute("unspecified_timestamp", DateTimeKind = DateTimeKind.Unspecified)]
    public DateTime? UnspecifiedTimestamp { get; set; }
    
    // DateTime without DateTimeKind specified (should default to Unspecified)
    [DynamoDbAttribute("default_timestamp")]
    public DateTime? DefaultTimestamp { get; set; }
    
    // DateTime with UTC kind and format string
    [DynamoDbAttribute("utc_date", DateTimeKind = DateTimeKind.Utc, Format = "yyyy-MM-dd")]
    public DateTime? UtcDate { get; set; }
    
    // DateTime with Local kind and format string
    [DynamoDbAttribute("local_datetime", DateTimeKind = DateTimeKind.Local, Format = "yyyy-MM-ddTHH:mm:ss")]
    public DateTime? LocalDateTime { get; set; }
}
