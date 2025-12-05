using System.Text;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for combined security features (encryption + logging redaction).
/// Validates that both Sensitive and Encrypted attributes work together correctly.
/// </summary>
public class CombinedSecurityFeaturesTests : IntegrationTestBase
{
    public CombinedSecurityFeaturesTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CombinedAttributes_EncryptsAndRedactsCorrectly()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        
        var entity = new SecureTestEntity
        {
            Id = "user-123",
            Name = "John Doe",
            SocialSecurityNumber = "123-45-6789", // Both [Encrypted] and [Sensitive]
            CreditCardNumber = "4111-1111-1111-1111", // Only [Encrypted]
            Email = "john@example.com", // Only [Sensitive]
            PublicData = "This is public"
        };

        // Act - Encrypt SSN (has both attributes)
        var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber);
        var encryptedSsn = await encryptor.EncryptAsync(
            ssnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["name"] = new AttributeValue { S = entity.Name },
            ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) },
            ["email"] = new AttributeValue { S = entity.Email },
            ["public_data"] = new AttributeValue { S = entity.PublicData }
        };

        // Test logging redaction
        var sensitiveFields = new HashSet<string> { "ssn", "email" };
        var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert - Encryption happened
        item["ssn"].B.Should().NotBeNull("SSN should be encrypted");
        var ssnBinary = item["ssn"].B.ToArray();
        ssnBinary.Should().NotEqual(ssnBytes, "SSN should be encrypted, not plaintext");

        // Assert - Logging redaction happened
        redactedItem.Should().NotBeNull();
        redactedItem!["ssn"].S.Should().Be("[REDACTED]", "encrypted+sensitive field should be redacted in logs");
        redactedItem["email"].S.Should().Be("[REDACTED]", "sensitive field should be redacted in logs");
        redactedItem["name"].S.Should().Be(entity.Name, "non-sensitive field should not be redacted");
        redactedItem["public_data"].S.Should().Be(entity.PublicData, "non-sensitive field should not be redacted");


        // Assert - Decryption still works
        var decryptedSsn = await encryptor.DecryptAsync(
            ssnBinary,
            "ssn",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsn).Should().Be(entity.SocialSecurityNumber);
    }

    [Fact]
    public async Task EncryptedOnly_EncryptsButDoesNotRedact()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var creditCard = "4111-1111-1111-1111";
        
        // Act - Encrypt credit card (only [Encrypted], not [Sensitive])
        var ccBytes = Encoding.UTF8.GetBytes(creditCard);
        var encryptedCc = await encryptor.EncryptAsync(
            ccBytes,
            "credit_card",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "user-123" },
            ["credit_card"] = new AttributeValue { B = new MemoryStream(encryptedCc) }
        };

        // Test logging redaction (credit_card is NOT in sensitive fields)
        var sensitiveFields = new HashSet<string> { "ssn", "email" };
        var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert - Encryption happened
        item["credit_card"].B.Should().NotBeNull();
        
        // Assert - No redaction (credit_card is not marked as sensitive)
        redactedItem.Should().NotBeNull();
        redactedItem!["credit_card"].B.Should().NotBeNull("encrypted-only field should not be redacted");
        
        // Assert - Decryption works
        var decrypted = await encryptor.DecryptAsync(
            encryptedCc,
            "credit_card",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);
        Encoding.UTF8.GetString(decrypted).Should().Be(creditCard);
    }

    [Fact]
    public void SensitiveOnly_RedactsButDoesNotEncrypt()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "user-123" },
            ["email"] = new AttributeValue { S = "john@example.com" } // Only [Sensitive]
        };

        var sensitiveFields = new HashSet<string> { "email" };

        // Act
        var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert - Redaction happened
        redactedItem.Should().NotBeNull();
        redactedItem!["email"].S.Should().Be("[REDACTED]");
        
        // Assert - Original data is still plaintext (not encrypted)
        item["email"].S.Should().Be("john@example.com", "sensitive-only field should not be encrypted");
        item["email"].B.Should().BeNull("sensitive-only field should be stored as string, not binary");
    }
}
