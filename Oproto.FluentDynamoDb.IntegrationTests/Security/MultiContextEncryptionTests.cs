using System.Text;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for multi-context encryption scenarios.
/// Validates that different contexts use different encryption keys and data isolation.
/// </summary>
public class MultiContextEncryptionTests : IntegrationTestBase
{
    public MultiContextEncryptionTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task MultiContext_DifferentContextsUseDifferentKeys()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = MockFieldEncryptor.CreateWithContextKeys("tenant-a", "tenant-b");
        var plaintext = Encoding.UTF8.GetBytes("sensitive-data");

        // Act - Encrypt same data for two different contexts
        var encryptedForTenantA = await encryptor.EncryptAsync(
            plaintext,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-a" },
            CancellationToken.None);

        var encryptedForTenantB = await encryptor.EncryptAsync(
            plaintext,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);

        // Assert - Encrypted data should be different
        encryptedForTenantA.Should().NotEqual(encryptedForTenantB,
            "different contexts should produce different ciphertext");

        // Decrypt with correct context - should succeed
        var decryptedA = await encryptor.DecryptAsync(
            encryptedForTenantA,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-a" },
            CancellationToken.None);
        decryptedA.Should().Equal(plaintext);

        var decryptedB = await encryptor.DecryptAsync(
            encryptedForTenantB,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);
        decryptedB.Should().Equal(plaintext);
    }

    [Fact]
    public async Task MultiContext_CannotDecryptWithWrongContext()
    {
        // Arrange
        var encryptor = MockFieldEncryptor.CreateWithContextKeys("tenant-a", "tenant-b");
        var plaintext = Encoding.UTF8.GetBytes("sensitive-data");

        // Act - Encrypt for tenant-a
        var encrypted = await encryptor.EncryptAsync(
            plaintext,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-a" },
            CancellationToken.None);

        // Assert - Attempting to decrypt with tenant-b context should fail
        var decryptWithWrongContext = async () => await encryptor.DecryptAsync(
            encrypted,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);

        await decryptWithWrongContext.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Encryption context mismatch*");
    }

    [Fact]
    public async Task MultiContext_IsolatesDataBetweenContexts()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = MockFieldEncryptor.CreateWithContextKeys("tenant-a", "tenant-b");

        // Create entities for different tenants
        var entityA = new SecureTestEntity
        {
            Id = "user-a",
            Name = "Tenant A User",
            SocialSecurityNumber = "111-11-1111"
        };

        var entityB = new SecureTestEntity
        {
            Id = "user-b",
            Name = "Tenant B User",
            SocialSecurityNumber = "222-22-2222"
        };

        // Act - Store encrypted data for both tenants
        var itemA = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entityA.Id },
            ["name"] = new AttributeValue { S = entityA.Name }
        };

        var ssnBytesA = Encoding.UTF8.GetBytes(entityA.SocialSecurityNumber!);
        var encryptedSsnA = await encryptor.EncryptAsync(
            ssnBytesA,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-a" },
            CancellationToken.None);
        itemA["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsnA) };

        var itemB = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entityB.Id },
            ["name"] = new AttributeValue { S = entityB.Name }
        };

        var ssnBytesB = Encoding.UTF8.GetBytes(entityB.SocialSecurityNumber!);
        var encryptedSsnB = await encryptor.EncryptAsync(
            ssnBytesB,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);
        itemB["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsnB) };

        await DynamoDb.PutItemAsync(TableName, itemA);
        await DynamoDb.PutItemAsync(TableName, itemB);

        // Retrieve and decrypt with correct contexts
        var retrievedA = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entityA.Id }
        });

        var retrievedB = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entityB.Id }
        });

        // Assert - Each tenant's data can only be decrypted with their context
        var decryptedSsnA = await encryptor.DecryptAsync(
            retrievedA.Item["ssn"].B.ToArray(),
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-a" },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsnA).Should().Be(entityA.SocialSecurityNumber);

        var decryptedSsnB = await encryptor.DecryptAsync(
            retrievedB.Item["ssn"].B.ToArray(),
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsnB).Should().Be(entityB.SocialSecurityNumber);

        // Verify cross-tenant decryption fails
        var crossDecrypt = async () => await encryptor.DecryptAsync(
            retrievedA.Item["ssn"].B.ToArray(),
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-b" },
            CancellationToken.None);

        await crossDecrypt.Should().ThrowAsync<InvalidOperationException>();
    }
}
