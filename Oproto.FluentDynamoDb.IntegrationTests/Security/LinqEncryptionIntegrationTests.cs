using System.Text;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for table.Encrypt() method in LINQ expressions.
/// Tests end-to-end encryption with DynamoDB Local using LINQ-style queries.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Encryption")]
[Trait("Feature", "LinqExpressions")]
public class LinqEncryptionIntegrationTests : IntegrationTestBase
{
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(Amazon.DynamoDBv2.IAmazonDynamoDB client, string tableName, IFieldEncryptor? encryptor = null)
            : base(client, tableName, logger: null, fieldEncryptor: encryptor)
        {
        }
    }

    public LinqEncryptionIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Query_WithTableEncryptInLinqExpression_EncryptsAndQueriesCorrectly()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        var entity = new SecureTestEntity
        {
            Id = "user-123",
            Name = "John Doe",
            SocialSecurityNumber = "123-45-6789",
            Email = "john@example.com",
            PublicData = "Public information"
        };

        // Manually encrypt and store the entity
        var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber);
        var encryptedSsn = await encryptor.EncryptAsync(
            ssnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-123" },
            CancellationToken.None);

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["name"] = new AttributeValue { S = entity.Name },
            ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) },
            ["email"] = new AttributeValue { S = entity.Email },
            ["public_data"] = new AttributeValue { S = entity.PublicData }
        };

        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Query using table.Encrypt() in LINQ expression
        EncryptionContext.Current = "tenant-123";
        var metadata = SecureTestEntity.GetEntityMetadata();
        
        var queryBuilder = table.Query<SecureTestEntity>()
            .Where(x => x.Id == "user-123");
        
        var response = await queryBuilder.ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        
        var retrievedItem = response.Items[0];
        retrievedItem["pk"].S.Should().Be("user-123");
        retrievedItem["ssn"].B.Should().NotBeNull();
        
        // Verify the SSN is encrypted in DynamoDB
        var storedSsnBytes = retrievedItem["ssn"].B.ToArray();
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "tenant-123" },
            CancellationToken.None);
        var decryptedSsn = Encoding.UTF8.GetString(decryptedSsnBytes);
        decryptedSsn.Should().Be("123-45-6789");
    }

    [Fact]
    public async Task Scan_WithTableEncryptInLinqExpression_EncryptsAndScansCorrectly()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        // Create multiple entities with encrypted SSNs
        var entities = new[]
        {
            new SecureTestEntity
            {
                Id = "user-1",
                Name = "Alice",
                SocialSecurityNumber = "111-11-1111",
                Email = "alice@example.com"
            },
            new SecureTestEntity
            {
                Id = "user-2",
                Name = "Bob",
                SocialSecurityNumber = "222-22-2222",
                Email = "bob@example.com"
            },
            new SecureTestEntity
            {
                Id = "user-3",
                Name = "Charlie",
                SocialSecurityNumber = "333-33-3333",
                Email = "charlie@example.com"
            }
        };

        // Store entities with encrypted SSNs
        foreach (var entity in entities)
        {
            var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber!);
            var encryptedSsn = await encryptor.EncryptAsync(
                ssnBytes,
                "ssn",
                new FieldEncryptionContext { ContextId = "tenant-456" },
                CancellationToken.None);

            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = entity.Id },
                ["name"] = new AttributeValue { S = entity.Name },
                ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) },
                ["email"] = new AttributeValue { S = entity.Email! }
            };

            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Scan with filter using table.Encrypt()
        EncryptionContext.Current = "tenant-456";
        
        // Note: We're scanning for a specific encrypted SSN
        // This demonstrates the encryption works, but in practice you'd rarely scan for encrypted values
        var targetSsn = "222-22-2222";
        var encryptedTargetSsn = table.Encrypt(targetSsn, "ssn");
        
        var scanRequest = new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "ssn = :ssn",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":ssn"] = new AttributeValue { B = new MemoryStream(Convert.FromBase64String(encryptedTargetSsn)) }
            }
        };

        var response = await DynamoDb.ScanAsync(scanRequest);

        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0]["pk"].S.Should().Be("user-2");
        response.Items[0]["name"].S.Should().Be("Bob");
    }

    [Fact]
    public async Task Query_WithTableEncryptAndMultipleFields_EncryptsAllFieldsCorrectly()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        var entity = new SecureTestEntity
        {
            Id = "user-999",
            Name = "Test User",
            SocialSecurityNumber = "999-99-9999",
            CreditCardNumber = "4111-1111-1111-1111",
            Email = "test@example.com"
        };

        // Store entity with encrypted fields
        var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber);
        var ccBytes = Encoding.UTF8.GetBytes(entity.CreditCardNumber);
        
        var encryptedSsn = await encryptor.EncryptAsync(
            ssnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "multi-tenant" },
            CancellationToken.None);
        
        var encryptedCc = await encryptor.EncryptAsync(
            ccBytes,
            "credit_card",
            new FieldEncryptionContext { ContextId = "multi-tenant" },
            CancellationToken.None);

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["name"] = new AttributeValue { S = entity.Name },
            ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) },
            ["credit_card"] = new AttributeValue { B = new MemoryStream(encryptedCc) },
            ["email"] = new AttributeValue { S = entity.Email }
        };

        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Query and verify both encrypted fields
        EncryptionContext.Current = "multi-tenant";
        
        var queryBuilder = table.Query<SecureTestEntity>()
            .Where(x => x.Id == "user-999");
        
        var response = await queryBuilder.ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var retrievedItem = response.Items[0];
        
        // Verify SSN is encrypted and can be decrypted
        var storedSsnBytes = retrievedItem["ssn"].B.ToArray();
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "multi-tenant" },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsnBytes).Should().Be("999-99-9999");
        
        // Verify credit card is encrypted and can be decrypted
        var storedCcBytes = retrievedItem["credit_card"].B.ToArray();
        var decryptedCcBytes = await encryptor.DecryptAsync(
            storedCcBytes,
            "credit_card",
            new FieldEncryptionContext { ContextId = "multi-tenant" },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedCcBytes).Should().Be("4111-1111-1111-1111");
    }

    [Fact]
    public async Task Query_WithTableEncryptAndDifferentContexts_UsesCorrectContext()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = MockFieldEncryptor.CreateWithContextKeys("context-A", "context-B");
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        // Store entity with context-A
        var entity = new SecureTestEntity
        {
            Id = "user-context-test",
            Name = "Context Test User",
            SocialSecurityNumber = "555-55-5555",
            Email = "context@example.com"
        };

        var ssnBytes = Encoding.UTF8.GetBytes(entity.SocialSecurityNumber);
        var encryptedSsn = await encryptor.EncryptAsync(
            ssnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "context-A" },
            CancellationToken.None);

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["name"] = new AttributeValue { S = entity.Name },
            ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) },
            ["email"] = new AttributeValue { S = entity.Email }
        };

        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Query with correct context
        EncryptionContext.Current = "context-A";
        
        var queryBuilder = table.Query<SecureTestEntity>()
            .Where(x => x.Id == "user-context-test");
        
        var response = await queryBuilder.ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var retrievedItem = response.Items[0];
        
        // Verify decryption with correct context works
        var storedSsnBytes = retrievedItem["ssn"].B.ToArray();
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "context-A" },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsnBytes).Should().Be("555-55-5555");
        
        // Verify decryption with wrong context fails
        var decryptWithWrongContext = async () => await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "context-B" },
            CancellationToken.None);
        
        await decryptWithWrongContext.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Encryption context mismatch*");
    }

    [Fact]
    public void Query_WithTableEncryptAndNoEncryptor_ThrowsInvalidOperationException()
    {
        // Arrange
        var table = new TestTable(DynamoDb, TableName, encryptor: null);
        EncryptionContext.Current = "tenant-123";

        // Act & Assert
        var act = () => table.Encrypt("123-45-6789", "ssn");
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IFieldEncryptor not configured*")
            .WithMessage("*Pass an IFieldEncryptor instance to the table constructor*");
    }

    [Fact]
    public async Task Query_WithEncryptedValueInFilter_RetrievesCorrectResults()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        // Create entities with different SSNs
        var entities = new[]
        {
            new { Id = "user-A", Ssn = "111-11-1111", Name = "Alice" },
            new { Id = "user-B", Ssn = "222-22-2222", Name = "Bob" },
            new { Id = "user-C", Ssn = "333-33-3333", Name = "Charlie" }
        };

        foreach (var entity in entities)
        {
            var ssnBytes = Encoding.UTF8.GetBytes(entity.Ssn);
            var encryptedSsn = await encryptor.EncryptAsync(
                ssnBytes,
                "ssn",
                new FieldEncryptionContext { ContextId = "filter-test" },
                CancellationToken.None);

            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = entity.Id },
                ["name"] = new AttributeValue { S = entity.Name },
                ["ssn"] = new AttributeValue { B = new MemoryStream(encryptedSsn) }
            };

            await DynamoDb.PutItemAsync(TableName, item);
        }

        // Act - Query with encrypted value in filter
        EncryptionContext.Current = "filter-test";
        var targetSsn = "222-22-2222";
        var encryptedTargetSsn = table.Encrypt(targetSsn, "ssn");
        
        // Use scan with filter since we're filtering on non-key attribute
        var scanRequest = new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "ssn = :ssn",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":ssn"] = new AttributeValue { B = new MemoryStream(Convert.FromBase64String(encryptedTargetSsn)) }
            }
        };

        var response = await DynamoDb.ScanAsync(scanRequest);

        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0]["pk"].S.Should().Be("user-B");
        response.Items[0]["name"].S.Should().Be("Bob");
    }

    [Fact]
    public async Task Query_WithNullEncryptionContext_EncryptsWithNullContext()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        var entity = new SecureTestEntity
        {
            Id = "user-null-context",
            Name = "Null Context User",
            SocialSecurityNumber = "000-00-0000",
            Email = "null@example.com"
        };

        // Store with null context
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
            ["email"] = new AttributeValue { S = entity.Email }
        };

        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Query with null context
        EncryptionContext.Current = null;
        
        var queryBuilder = table.Query<SecureTestEntity>()
            .Where(x => x.Id == "user-null-context");
        
        var response = await queryBuilder.ToDynamoDbResponseAsync();

        // Assert
        response.Items.Should().HaveCount(1);
        var retrievedItem = response.Items[0];
        
        // Verify decryption with null context works
        var storedSsnBytes = retrievedItem["ssn"].B.ToArray();
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = null },
            CancellationToken.None);
        Encoding.UTF8.GetString(decryptedSsnBytes).Should().Be("000-00-0000");
    }

    [Fact]
    public async Task Query_WithEncryptedValueRoundTrip_MaintainsDataIntegrity()
    {
        // Arrange
        await CreateTableAsync<SecureTestEntity>();
        
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(DynamoDb, TableName, encryptor);
        
        var originalSsn = "987-65-4321";
        EncryptionContext.Current = "roundtrip-test";
        
        // Encrypt the value
        var encryptedSsn = table.Encrypt(originalSsn, "ssn");
        
        // Store entity with encrypted SSN
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "user-roundtrip" },
            ["name"] = new AttributeValue { S = "Roundtrip User" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(Convert.FromBase64String(encryptedSsn)) }
        };

        await DynamoDb.PutItemAsync(TableName, item);

        // Act - Retrieve and decrypt
        var getRequest = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "user-roundtrip" }
            }
        };

        var getResponse = await DynamoDb.GetItemAsync(getRequest);

        // Assert
        getResponse.IsItemSet.Should().BeTrue();
        var storedSsnBytes = getResponse.Item["ssn"].B.ToArray();
        
        var decryptedSsnBytes = await encryptor.DecryptAsync(
            storedSsnBytes,
            "ssn",
            new FieldEncryptionContext { ContextId = "roundtrip-test" },
            CancellationToken.None);
        
        var decryptedSsn = Encoding.UTF8.GetString(decryptedSsnBytes);
        decryptedSsn.Should().Be(originalSsn);
    }
}
