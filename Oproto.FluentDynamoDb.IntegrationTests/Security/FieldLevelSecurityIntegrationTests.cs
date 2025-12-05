using System.Text;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for field-level security features including encryption and logging redaction.
/// Tests end-to-end encryption with DynamoDB Local and validates security attribute behavior.
/// </summary>
public class FieldLevelSecurityIntegrationTests : IntegrationTestBase
{
    public FieldLevelSecurityIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task EndToEndEncryption_WithEncryptedFields_StoresAndRetrievesCorrectly()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        
        var entity = new SecureTestEntity
        {
            Id = "user-123",
            Name = "John Doe",
            SocialSecurityNumber = "123-45-6789",
            CreditCardNumber = "4111-1111-1111-1111",
            Email = "john@example.com",
            PublicData = "This is public"
        };

        // Act - Manually encrypt fields and store
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["name"] = new AttributeValue { S = entity.Name },
            ["public_data"] = new AttributeValue { S = entity.PublicData },
            ["email"] = new AttributeValue { S = entity.Email }
        };

        // Encrypt SSN
        if (entity.SocialSecurityNumber != null)
        {
            var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber);
            var encryptedSsn = await encryptor.EncryptAsync(
                ssnBytes,
                "ssn",
                new FieldEncryptionContext { ContextId = null },
                CancellationToken.None);
            item["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) };
        }

        // Encrypt credit card
        if (entity.CreditCardNumber != null)
        {
            var ccBytes = Encoding.UTF8.GetBytes(entity.CreditCardNumber);
            var encryptedCc = await encryptor.EncryptAsync(
                ccBytes,
                "credit_card",
                new FieldEncryptionContext { ContextId = null },
                CancellationToken.None);
            item["credit_card"] = new AttributeValue { B = new MemoryStream(encryptedCc) };
        }

        await DynamoDb.PutItemAsync(TableName, item);

        // Verify encrypted format in DynamoDB
        var getResponse = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id }
        });

        getResponse.IsItemSet.Should().BeTrue();
        var storedItem = getResponse.Item;

        // Verify encrypted fields are stored as Binary type
        storedItem["ssn"].B.Should().NotBeNull();
        storedItem["credit_card"].B.Should().NotBeNull();

        // Verify encrypted data is not plaintext
        var ssnBinary = storedItem["ssn"].B.ToArray();
        var ssnPlaintext = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber!);
        ssnBinary.Should().NotEqual(ssnPlaintext, "SSN should be encrypted");

        // Decrypt and verify
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            ssnBinary,
            "ssn",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);
        var decryptedSsn = Encoding.UTF8.GetString(decryptedSsnBytes);
        decryptedSsn.Should().Be(entity.SocialSecurityNumber);

        var ccBinary = storedItem["credit_card"].B.ToArray();
        var decryptedCcBytes = await encryptor.DecryptAsync(
            ccBinary,
            "credit_card",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);
        var decryptedCc = Encoding.UTF8.GetString(decryptedCcBytes);
        decryptedCc.Should().Be(entity.CreditCardNumber);

        // Verify non-encrypted fields are plaintext
        storedItem["name"].S.Should().Be(entity.Name);
        storedItem["email"].S.Should().Be(entity.Email);
        storedItem["public_data"].S.Should().Be(entity.PublicData);
    }

    [Fact]
    public async Task EncryptionContext_IsPreservedInEncryptedData()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var contextId = "tenant-123";
        
        var plaintext = Encoding.UTF8.GetBytes("sensitive-data");

        // Act - Encrypt with context
        var encrypted = await encryptor.EncryptAsync(
            plaintext,
            "ssn",
            new FieldEncryptionContext { ContextId = contextId },
            CancellationToken.None);

        // Decrypt with same context - should succeed
        var decrypted = await encryptor.DecryptAsync(
            encrypted,
            "ssn",
            new FieldEncryptionContext { ContextId = contextId },
            CancellationToken.None);

        // Assert
        decrypted.Should().Equal(plaintext);

        // Decrypt with different field name - should fail
        var decryptWithWrongField = async () => await encryptor.DecryptAsync(
            encrypted,
            "credit_card", // Wrong field name
            new FieldEncryptionContext { ContextId = contextId },
            CancellationToken.None);

        await decryptWithWrongField.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Encryption context mismatch*");

        // Decrypt with different context - should fail
        var decryptWithWrongContext = async () => await encryptor.DecryptAsync(
            encrypted,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-456" }, // Wrong context
            CancellationToken.None);

        await decryptWithWrongContext.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Encryption context mismatch*");
    }

    [Fact]
    public async Task EncryptedData_UsesAwsEncryptionSdkMessageFormat()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var plaintext = Encoding.UTF8.GetBytes("test-data");

        // Act
        var encrypted = await encryptor.EncryptAsync(
            plaintext,
            "test_field",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);

        // Assert - Verify message format structure
        encrypted.Should().NotBeEmpty();
        encrypted.Length.Should().BeGreaterThan(plaintext.Length, "encrypted data includes metadata");

        // Verify version byte
        encrypted[0].Should().Be(1, "message format version should be 1");

        // Verify we can decrypt it
        var decrypted = await encryptor.DecryptAsync(
            encrypted,
            "test_field",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);

        decrypted.Should().Equal(plaintext);
    }
}
