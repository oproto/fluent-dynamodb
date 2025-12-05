using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with sensitive fields for security integration testing.
/// Note: Encrypted attributes are commented out since encryption package is not referenced in tests.
/// </summary>
[DynamoDbTable("test-secure-entity")]
public partial class SecureTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    // [Encrypted] - Commented out for testing without encryption package
    [Sensitive]
    [DynamoDbAttribute("ssn")]
    public string? SocialSecurityNumber { get; set; }
    
    // [Encrypted] - Commented out for testing without encryption package
    [DynamoDbAttribute("credit_card")]
    public string? CreditCardNumber { get; set; }
    
    [Sensitive]
    [DynamoDbAttribute("email")]
    public string? Email { get; set; }
    
    [DynamoDbAttribute("public_data")]
    public string? PublicData { get; set; }
}
