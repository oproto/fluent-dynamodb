using System.Text;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for encryption round-trips with update expressions.
/// Tests the deferred encryption feature where parameters marked as requiring encryption
/// are automatically encrypted by the request builder before sending to DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Encryption")]
public class EncryptionRoundTripTests : IntegrationTestBase
{
    public EncryptionRoundTripTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<EncryptedTestEntity>();
    }

    [Fact]
    public async Task UpdateExpression_WithEncryptedParameter_EncryptsBeforeSending()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var entity = new EncryptedTestEntity
        {
            Id = "user-001",
            Type = "customer",
            Name = "John Doe",
            Email = "john@example.com",
            SocialSecurityNumber = "123-45-6789"
        };

        await SaveAsync(entity);

        // Act - Update with deferred encryption
        var builder = new UpdateItemRequestBuilder<EncryptedTestEntity>(DynamoDb, (FluentDynamoDbOptions?)null)
            .ForTable(TableName)
            .SetFieldEncryptor(encryptor)
            .WithKey("pk", entity.Id, "sk", entity.Type!);
        
        var metadata = CreateMetadata("SocialSecurityNumber", "ssn", isEncrypted: true);
        var context = CreateContext(builder, metadata);
        
        // Mark parameter as requiring encryption
        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "987-65-4321" },
            RequiresEncryption = true,
            PropertyName = "SocialSecurityNumber",
            AttributeName = "ssn"
        });
        
        builder.SetExpressionContext(context);
        builder.Set("SET #ssn = :p0")
            .WithAttribute("#ssn", "ssn")
            .WithValue(":p0", "987-65-4321");
        
        await builder.UpdateAsync();

        // Assert
        var storedItem = await GetItemAsync(entity.Id, entity.Type);
        storedItem["ssn"].B.Should().NotBeNull("SSN should be stored as binary");
        
        var encryptedBytes = storedItem["ssn"].B.ToArray();
        var plaintext = Encoding.UTF8.GetBytes("987-65-4321");
        encryptedBytes.Should().NotEqual(plaintext, "SSN should be encrypted");
        
        var decrypted = await encryptor.DecryptAsync(encryptedBytes, "SocialSecurityNumber", 
            new FieldEncryptionContext { ContextId = null }, CancellationToken.None);
        Encoding.UTF8.GetString(decrypted).Should().Be("987-65-4321");
    }

    [Fact]
    public async Task UpdateExpression_WithMultipleEncryptedParameters_EncryptsAll()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var entity = new EncryptedTestEntity
        {
            Id = "user-002",
            Type = "customer",
            Name = "Jane Smith",
            SocialSecurityNumber = "111-22-3333",
            CreditCardNumber = "4111-1111-1111-1111"
        };

        await SaveAsync(entity);

        // Act
        var builder = new UpdateItemRequestBuilder<EncryptedTestEntity>(DynamoDb, (FluentDynamoDbOptions?)null)
            .ForTable(TableName)
            .SetFieldEncryptor(encryptor)
            .WithKey("pk", entity.Id, "sk", entity.Type!);
        
        var metadata = CreateMetadata(
            ("SocialSecurityNumber", "ssn", true),
            ("CreditCardNumber", "credit_card", true));
        var context = CreateContext(builder, metadata);
        
        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "999-88-7777" },
            RequiresEncryption = true,
            PropertyName = "SocialSecurityNumber",
            AttributeName = "ssn"
        });
        
        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p1",
            Value = new AttributeValue { S = "5555-5555-5555-4444" },
            RequiresEncryption = true,
            PropertyName = "CreditCardNumber",
            AttributeName = "credit_card"
        });
        
        builder.SetExpressionContext(context);
        builder.Set("SET #ssn = :p0, #cc = :p1")
            .WithAttribute("#ssn", "ssn")
            .WithAttribute("#cc", "credit_card")
            .WithValue(":p0", "999-88-7777")
            .WithValue(":p1", "5555-5555-5555-4444");
        
        await builder.UpdateAsync();

        // Assert
        var storedItem = await GetItemAsync(entity.Id, entity.Type);
        
        storedItem["ssn"].B.Should().NotBeNull();
        var ssnDecrypted = await encryptor.DecryptAsync(storedItem["ssn"].B.ToArray(), "SocialSecurityNumber",
            new FieldEncryptionContext { ContextId = null }, CancellationToken.None);
        Encoding.UTF8.GetString(ssnDecrypted).Should().Be("999-88-7777");
        
        storedItem["credit_card"].B.Should().NotBeNull();
        var ccDecrypted = await encryptor.DecryptAsync(storedItem["credit_card"].B.ToArray(), "CreditCardNumber",
            new FieldEncryptionContext { ContextId = null }, CancellationToken.None);
        Encoding.UTF8.GetString(ccDecrypted).Should().Be("5555-5555-5555-4444");
    }

    [Fact]
    public async Task UpdateExpression_WithoutEncryptor_ThrowsException()
    {
        // Arrange
        var entity = new EncryptedTestEntity
        {
            Id = "user-003",
            Type = "customer",
            Name = "Bob Johnson",
            SocialSecurityNumber = "222-33-4444"
        };

        await SaveAsync(entity);

        // Act & Assert
        var builder = new UpdateItemRequestBuilder<EncryptedTestEntity>(DynamoDb, (FluentDynamoDbOptions?)null)
            .ForTable(TableName)
            .WithKey("pk", entity.Id, "sk", entity.Type!);
        
        var metadata = CreateMetadata("SocialSecurityNumber", "ssn", isEncrypted: true);
        var context = CreateContext(builder, metadata);
        
        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "666-77-8888" },
            RequiresEncryption = true,
            PropertyName = "SocialSecurityNumber",
            AttributeName = "ssn"
        });
        
        builder.SetExpressionContext(context);
        builder.Set("SET #ssn = :p0")
            .WithAttribute("#ssn", "ssn")
            .WithValue(":p0", "666-77-8888");
        
        var act = async () => await builder.UpdateAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Field encryption is required*but no IFieldEncryptor is configured*");
    }

    // Helper methods
    private async Task SaveAsync(EncryptedTestEntity entity)
    {
        var encryptor = new MockFieldEncryptor();
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = entity.Id },
            ["sk"] = new AttributeValue { S = entity.Type! }
        };
        
        if (entity.Name != null)
            item["name"] = new AttributeValue { S = entity.Name };
        if (entity.Email != null)
            item["email"] = new AttributeValue { S = entity.Email };

        if (entity.SocialSecurityNumber != null)
        {
            var encrypted = await encryptor.EncryptAsync(
                Encoding.UTF8.GetBytes(entity.SocialSecurityNumber),
                "SocialSecurityNumber",
                new FieldEncryptionContext { ContextId = null },
                CancellationToken.None);
            item["ssn"] = new AttributeValue { B = new MemoryStream(encrypted) };
        }

        if (entity.CreditCardNumber != null)
        {
            var encrypted = await encryptor.EncryptAsync(
                Encoding.UTF8.GetBytes(entity.CreditCardNumber),
                "CreditCardNumber",
                new FieldEncryptionContext { ContextId = null },
                CancellationToken.None);
            item["credit_card"] = new AttributeValue { B = new MemoryStream(encrypted) };
        }

        await DynamoDb.PutItemAsync(TableName, item);
    }

    private async Task<Dictionary<string, AttributeValue>> GetItemAsync(string pk, string sk)
    {
        var response = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        });
        
        response.IsItemSet.Should().BeTrue();
        return response.Item;
    }

    private EntityMetadata CreateMetadata(string propertyName, string attributeName, bool isEncrypted)
    {
        return new EntityMetadata
        {
            TableName = TableName,
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = propertyName,
                    AttributeName = attributeName,
                    PropertyType = typeof(string),
                    IsEncrypted = isEncrypted
                }
            }
        };
    }

    private EntityMetadata CreateMetadata(params (string propertyName, string attributeName, bool isEncrypted)[] properties)
    {
        return new EntityMetadata
        {
            TableName = TableName,
            Properties = properties.Select(p => new PropertyMetadata
            {
                PropertyName = p.propertyName,
                AttributeName = p.attributeName,
                PropertyType = typeof(string),
                IsEncrypted = p.isEncrypted
            }).ToArray()
        };
    }

    private ExpressionContext CreateContext(UpdateItemRequestBuilder<EncryptedTestEntity> builder, EntityMetadata metadata)
    {
        return new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            metadata,
            ExpressionValidationMode.None);
    }
}
