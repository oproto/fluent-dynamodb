using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity combining DateTime Kind, format strings, and encryption
/// for comprehensive integration testing of all features together.
/// </summary>
[DynamoDbTable("test-combined-features-entity")]
public partial class CombinedFeaturesEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string? Type { get; set; }
    
    // DateTime with UTC kind and format string
    [DynamoDbAttribute("created_utc", DateTimeKind = DateTimeKind.Utc, Format = "yyyy-MM-dd")]
    public DateTime? CreatedUtc { get; set; }
    
    // DateTime with Local kind and format string
    [DynamoDbAttribute("updated_local", DateTimeKind = DateTimeKind.Local, Format = "yyyy-MM-ddTHH:mm:ss")]
    public DateTime? UpdatedLocal { get; set; }
    
    // Decimal with format string
    [DynamoDbAttribute("salary", Format = "F2")]
    public decimal? Salary { get; set; }
    
    // DateTime with UTC kind (no format)
    [DynamoDbAttribute("birth_date_utc", DateTimeKind = DateTimeKind.Utc)]
    public DateTime? BirthDateUtc { get; set; }
    
    // DateTime with format string (no kind specified)
    [DynamoDbAttribute("expiry_date", Format = "yyyy-MM-dd")]
    public DateTime? ExpiryDate { get; set; }
    
    // Decimal with format string
    [DynamoDbAttribute("credit_limit", Format = "F4")]
    public decimal? CreditLimit { get; set; }
    
    // DateTime with both Kind and Format
    [DynamoDbAttribute("last_login", DateTimeKind = DateTimeKind.Utc, Format = "yyyy-MM-ddTHH:mm:ss")]
    public DateTime? LastLogin { get; set; }
    
    // Non-encrypted, non-formatted properties for comparison
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}
