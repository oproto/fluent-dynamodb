using Amazon.DynamoDBv2;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using System.Text;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Tests for table.Encrypt() and table.EncryptValue() methods.
/// </summary>
public class DynamoDbTableEncryptionTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Ssn { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, IFieldEncryptor? encryptor = null)
            : base(client, "TestTable", logger: null, fieldEncryptor: encryptor)
        {
        }
    }

    private class MockFieldEncryptor : IFieldEncryptor
    {
        public List<(byte[] Plaintext, string FieldName, FieldEncryptionContext Context)> EncryptCalls { get; } = new();
        public List<(byte[] Ciphertext, string FieldName, FieldEncryptionContext Context)> DecryptCalls { get; } = new();

        public Task<byte[]> EncryptAsync(
            byte[] plaintext,
            string fieldName,
            FieldEncryptionContext context,
            CancellationToken cancellationToken = default)
        {
            EncryptCalls.Add((plaintext, fieldName, context));
            
            // Return a simple "encrypted" value (just prefix with "ENCRYPTED:")
            var encrypted = Encoding.UTF8.GetBytes($"ENCRYPTED:{Encoding.UTF8.GetString(plaintext)}");
            return Task.FromResult(encrypted);
        }

        public Task<byte[]> DecryptAsync(
            byte[] ciphertext,
            string fieldName,
            FieldEncryptionContext context,
            CancellationToken cancellationToken = default)
        {
            DecryptCalls.Add((ciphertext, fieldName, context));
            
            // Return a simple "decrypted" value (just remove "ENCRYPTED:" prefix)
            var text = Encoding.UTF8.GetString(ciphertext);
            var decrypted = Encoding.UTF8.GetBytes(text.Replace("ENCRYPTED:", ""));
            return Task.FromResult(decrypted);
        }
    }

    [Fact]
    public void Encrypt_WithValidEncryptor_EncryptsValueCorrectly()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        var value = "123-45-6789";

        // Act
        var result = table.Encrypt(value, "Ssn");

        // Assert
        result.Should().NotBeNullOrEmpty();
        encryptor.EncryptCalls.Should().HaveCount(1);
        
        var call = encryptor.EncryptCalls[0];
        Encoding.UTF8.GetString(call.Plaintext).Should().Be("123-45-6789");
        call.FieldName.Should().Be("Ssn");
        call.Context.ContextId.Should().Be("tenant-123");
        call.Context.CacheTtlSeconds.Should().Be(300); // Default value
        
        // Result should be base64-encoded
        var decoded = Convert.FromBase64String(result);
        Encoding.UTF8.GetString(decoded).Should().Be("ENCRYPTED:123-45-6789");
    }

    [Fact]
    public void Encrypt_WithNoEncryptor_ThrowsInvalidOperationException()
    {
        // Arrange
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor: null);
        var value = "123-45-6789";

        // Act
        var act = () => table.Encrypt(value, "Ssn");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IFieldEncryptor not configured*")
            .WithMessage("*Pass an IFieldEncryptor instance to the table constructor*");
    }

    [Fact]
    public void Encrypt_UsesAmbientDynamoDbOperationContext()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "customer-456" };
        var value = "secret-data";

        // Act
        var result = table.Encrypt(value, "SecretField");

        // Assert
        var call = encryptor.EncryptCalls[0];
        call.Context.ContextId.Should().Be("customer-456");
    }

    [Fact]
    public void Encrypt_WithNullAmbientContext_PassesNullContextId()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = null;
        var value = "data";

        // Act
        var result = table.Encrypt(value, "Field");

        // Assert
        var call = encryptor.EncryptCalls[0];
        call.Context.ContextId.Should().BeNull();
    }

    [Fact]
    public void Encrypt_BuildsFieldEncryptionContextCorrectly()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "region-us-east-1" };
        var value = "test-value";

        // Act
        var result = table.Encrypt(value, "TestField");

        // Assert
        var call = encryptor.EncryptCalls[0];
        call.Context.Should().NotBeNull();
        call.Context.ContextId.Should().Be("region-us-east-1");
        call.Context.CacheTtlSeconds.Should().Be(300); // Default from generated code
        call.Context.IsExternalBlob.Should().BeFalse(); // Default
        call.Context.EntityId.Should().BeNull(); // Not set for query encryption
    }

    [Fact]
    public void Encrypt_WithNullValue_EncryptsEmptyString()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };

        // Act
        var result = table.Encrypt(null, "Field");

        // Assert
        var call = encryptor.EncryptCalls[0];
        Encoding.UTF8.GetString(call.Plaintext).Should().Be("");
    }

    [Fact]
    public void Encrypt_WithNumericValue_ConvertsToString()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        var value = 12345;

        // Act
        var result = table.Encrypt(value, "NumericField");

        // Assert
        var call = encryptor.EncryptCalls[0];
        Encoding.UTF8.GetString(call.Plaintext).Should().Be("12345");
    }

    [Fact]
    public void Encrypt_WhenEncryptorThrows_WrapsInExpressionTranslationException()
    {
        // Arrange
        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns<Task<byte[]>>(_ => throw new Exception("Encryption failed"));

        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };

        // Act
        var act = () => table.Encrypt("value", "Field");

        // Assert
        act.Should().Throw<Exception>()
            .WithMessage("*Encryption failed*");
    }

    [Fact]
    public void Encrypt_InLinqExpression_IsDetectedByExpressionTranslator()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        
        var translator = new ExpressionTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Ssn",
                    AttributeName = "ssn",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                }
            }
        };
        var context = new ExpressionContext(
            new AttributeValueInternal(),
            new AttributeNameInternal(),
            metadata,
            ExpressionValidationMode.None);

        var ssn = "123-45-6789";

        // Act
        var result = translator.Translate<TestEntity>(
            x => x.Ssn == table.Encrypt(ssn, "Ssn"),
            context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("ssn");
        
        // The encrypted value should be captured as a parameter
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        var encryptedValue = context.AttributeValues.AttributeValues[":p0"].S;
        
        // Verify it's base64-encoded encrypted data
        var decoded = Convert.FromBase64String(encryptedValue);
        Encoding.UTF8.GetString(decoded).Should().Be("ENCRYPTED:123-45-6789");
        
        // Verify encryptor was called
        encryptor.EncryptCalls.Should().HaveCount(1);
        encryptor.EncryptCalls[0].FieldName.Should().Be("Ssn");
    }

    [Fact]
    public void Encrypt_InComplexLinqExpression_WorksCorrectly()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        
        var translator = new ExpressionTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Ssn",
                    AttributeName = "ssn",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                }
            }
        };
        var context = new ExpressionContext(
            new AttributeValueInternal(),
            new AttributeNameInternal(),
            metadata,
            ExpressionValidationMode.None);

        var ssn = "123-45-6789";
        var email = "test@example.com";

        // Act
        var result = translator.Translate<TestEntity>(
            x => x.Ssn == table.Encrypt(ssn, "Ssn") && x.Email == table.Encrypt(email, "Email"),
            context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 = :p1)");
        
        // Verify both values were encrypted
        encryptor.EncryptCalls.Should().HaveCount(2);
        encryptor.EncryptCalls[0].FieldName.Should().Be("Ssn");
        encryptor.EncryptCalls[1].FieldName.Should().Be("Email");
    }

    [Fact]
    public void EncryptValue_WithValidEncryptor_EncryptsValueCorrectly()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        var value = "secret-data";

        // Act
        var result = table.EncryptValue(value, "SecretField");

        // Assert
        result.Should().NotBeNullOrEmpty();
        encryptor.EncryptCalls.Should().HaveCount(1);
        
        var call = encryptor.EncryptCalls[0];
        Encoding.UTF8.GetString(call.Plaintext).Should().Be("secret-data");
        call.FieldName.Should().Be("SecretField");
        call.Context.ContextId.Should().Be("tenant-123");
    }

    [Fact]
    public void EncryptValue_IsAliasForEncrypt()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        var value = "test-value";

        // Act
        var result1 = table.Encrypt(value, "Field");
        encryptor.EncryptCalls.Clear();
        var result2 = table.EncryptValue(value, "Field");

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void EncryptValue_CanBeUsedInLinqExpression()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        
        var translator = new ExpressionTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Ssn",
                    AttributeName = "ssn",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                }
            }
        };
        var context = new ExpressionContext(
            new AttributeValueInternal(),
            new AttributeNameInternal(),
            metadata,
            ExpressionValidationMode.None);

        // Pre-encrypt the value
        var encryptedSsn = table.EncryptValue("123-45-6789", "Ssn");

        // Act
        var result = translator.Translate<TestEntity>(
            x => x.Ssn == encryptedSsn,
            context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be(encryptedSsn);
        
        // Verify encryptor was called once (during pre-encryption)
        encryptor.EncryptCalls.Should().HaveCount(1);
    }

    [Fact]
    public void Encrypt_WithMultipleCalls_UsesCorrectContextForEach()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);

        // Act & Assert - First call with context A
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "context-A" };
        var result1 = table.Encrypt("value1", "Field1");
        encryptor.EncryptCalls[0].Context.ContextId.Should().Be("context-A");

        // Act & Assert - Second call with context B
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "context-B" };
        var result2 = table.Encrypt("value2", "Field2");
        encryptor.EncryptCalls[1].Context.ContextId.Should().Be("context-B");

        // Verify both calls were made
        encryptor.EncryptCalls.Should().HaveCount(2);
    }

    [Fact]
    public void Encrypt_ReturnsBase64EncodedString()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };

        // Act
        var result = table.Encrypt("test", "Field");

        // Assert
        result.Should().NotBeNullOrEmpty();
        
        // Should be valid base64
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void Encrypt_WithDifferentFieldNames_PassesCorrectFieldName()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };

        // Act
        table.Encrypt("value1", "Ssn");
        table.Encrypt("value2", "Email");
        table.Encrypt("value3", "CreditCard");

        // Assert
        encryptor.EncryptCalls.Should().HaveCount(3);
        encryptor.EncryptCalls[0].FieldName.Should().Be("Ssn");
        encryptor.EncryptCalls[1].FieldName.Should().Be("Email");
        encryptor.EncryptCalls[2].FieldName.Should().Be("CreditCard");
    }

    [Fact]
    public void EncryptValue_WithNoEncryptor_ThrowsInvalidOperationException()
    {
        // Arrange
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor: null);
        var value = "secret-data";

        // Act
        var act = () => table.EncryptValue(value, "SecretField");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IFieldEncryptor not configured*")
            .WithMessage("*Pass an IFieldEncryptor instance to the table constructor*");
    }

    [Fact]
    public void EncryptValue_CanBeUsedInFormatStringExpression()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        
        // Pre-encrypt the value
        var encryptedSsn = table.EncryptValue("123-45-6789", "Ssn");

        // Act - Use in format string expression (simulated via QueryRequestBuilder)
        var queryBuilder = new QueryRequestBuilder<TestEntity>(
            Substitute.For<IAmazonDynamoDB>());
        
        queryBuilder
            .ForTable("TestTable")
            .Where("ssn = :val")
            .WithValue(":val", encryptedSsn);
        
        var request = queryBuilder.ToQueryRequest();

        // Assert
        request.ExpressionAttributeValues.Should().ContainKey(":val");
        request.ExpressionAttributeValues[":val"].S.Should().Be(encryptedSsn);
        
        // Verify encryptor was called once (during pre-encryption)
        encryptor.EncryptCalls.Should().HaveCount(1);
        encryptor.EncryptCalls[0].FieldName.Should().Be("Ssn");
    }

    [Fact]
    public void EncryptValue_WithFormatStringPlaceholder_WorksCorrectly()
    {
        // Arrange
        var encryptor = new MockFieldEncryptor();
        var table = new TestTable(Substitute.For<IAmazonDynamoDB>(), encryptor);
        DynamoDbOperationContext.Current = new OperationContextData { EncryptionContextId = "tenant-123" };
        
        // Pre-encrypt the value
        var encryptedEmail = table.EncryptValue("user@example.com", "Email");

        // Act - Use in format string with placeholder
        var queryBuilder = new QueryRequestBuilder<TestEntity>(
            Substitute.For<IAmazonDynamoDB>());
        
        queryBuilder
            .ForTable("TestTable")
            .Where($"email = {encryptedEmail}");
        
        // Assert
        encryptedEmail.Should().NotBeNullOrEmpty();
        
        // Verify encryptor was called
        encryptor.EncryptCalls.Should().HaveCount(1);
        encryptor.EncryptCalls[0].FieldName.Should().Be("Email");
        
        // Verify the encrypted value is base64-encoded
        var decoded = Convert.FromBase64String(encryptedEmail);
        Encoding.UTF8.GetString(decoded).Should().Be("ENCRYPTED:user@example.com");
    }
}
