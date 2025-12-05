using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with encrypted properties for testing deferred encryption in update expressions.
/// </summary>
[DynamoDbTable("test-encrypted-entity")]
public partial class EncryptedTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string? Type { get; set; }
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("email")]
    public string? Email { get; set; }
    
    [Encrypted]
    [Sensitive]
    [DynamoDbAttribute("ssn")]
    public string? SocialSecurityNumber { get; set; }
    
    [Encrypted]
    [DynamoDbAttribute("credit_card")]
    public string? CreditCardNumber { get; set; }
    
    [Encrypted]
    [DynamoDbAttribute("salary", Format = "F2")]
    public decimal? Salary { get; set; }
}
