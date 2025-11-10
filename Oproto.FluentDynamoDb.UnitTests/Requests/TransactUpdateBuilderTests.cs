using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactUpdateBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.TableName.Should().Be("TestTable");
    }

    #region Keys

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Keys.Should().HaveCount(1);
        req.Update.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Should().ContainKey("sk");
        req.Update.Key.Keys.Should().HaveCount(2);
        req.Update.Key["pk"].S.Should().Be("1");
        req.Update.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Should().ContainKey("sk");
        req.Update.Key.Keys.Should().HaveCount(2);
        req.Update.Key["pk"].S.Should().Be("1");
        req.Update.Key["sk"].S.Should().Be("abcd");
    }

    #endregion Keys

    #region Attributes

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValue(":pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }

    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValue(":pk", true);
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes

    [Fact]
    public void SetSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.Set("SET #pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.UpdateExpression.Should().Be("SET #pk = :pk");
    }

    [Fact]
    public void WhereSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.Where("#pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ConditionExpression.Should().Be("#pk = :pk");
    }

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ReturnValuesOnConditionCheckFailure.Should().Be(Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }

    #region Encryption Tests

    [Fact]
    public async Task EncryptParametersAsync_WithEncryptedParameter_EncryptsValue()
    {
        // Arrange
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new TransactUpdateBuilder("TestTable")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with encrypted parameter
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Setup mock encryptor to return encrypted bytes
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        // Act
        await builder.EncryptParametersAsync();
        var req = builder.ToWriteItem();

        // Assert
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "SensitiveField",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify the request has encrypted value (as binary)
        req.Update.ExpressionAttributeValues[":p0"].B.Should().NotBeNull();
        req.Update.ExpressionAttributeValues[":p0"].B.ToArray().Should().Equal(encryptedBytes);
    }

    [Fact]
    public async Task EncryptParametersAsync_WithoutEncryptor_ThrowsException()
    {
        // Arrange
        var builder = new TransactUpdateBuilder("TestTable");

        // Create expression context with encrypted parameter but no encryptor
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.EncryptParametersAsync());

        exception.Message.Should().Contain("IFieldEncryptor");
        exception.Message.Should().Contain("SensitiveField");
    }

    [Fact]
    public async Task EncryptParametersAsync_WithMultipleEncryptedParameters_EncryptsAll()
    {
        // Arrange
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new TransactUpdateBuilder("TestTable")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with multiple encrypted parameters
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data-1" },
            RequiresEncryption = true,
            PropertyName = "Field1",
            AttributeName = "field1"
        });

        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p1",
            Value = new AttributeValue { S = "sensitive-data-2" },
            RequiresEncryption = true,
            PropertyName = "Field2",
            AttributeName = "field2"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field1 = :p0, #field2 = :p1")
            .WithAttribute("#field1", "field1")
            .WithAttribute("#field2", "field2")
            .WithValue(":p0", "sensitive-data-1")
            .WithValue(":p1", "sensitive-data-2");

        // Setup mock encryptor
        var encryptedBytes1 = System.Text.Encoding.UTF8.GetBytes("encrypted-1");
        var encryptedBytes2 = System.Text.Encoding.UTF8.GetBytes("encrypted-2");
        
        mockEncryptor.EncryptAsync(
            Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "sensitive-data-1"),
            "Field1",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes1));

        mockEncryptor.EncryptAsync(
            Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "sensitive-data-2"),
            "Field2",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes2));

        // Act
        await builder.EncryptParametersAsync();
        var req = builder.ToWriteItem();

        // Assert
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "Field1",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "Field2",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify both values are encrypted (as binary)
        req.Update.ExpressionAttributeValues[":p0"].B.Should().NotBeNull();
        req.Update.ExpressionAttributeValues[":p0"].B.ToArray().Should().Equal(encryptedBytes1);
        req.Update.ExpressionAttributeValues[":p1"].B.Should().NotBeNull();
        req.Update.ExpressionAttributeValues[":p1"].B.ToArray().Should().Equal(encryptedBytes2);
    }

    [Fact]
    public async Task EncryptParametersAsync_WithEncryptionFailure_ThrowsFieldEncryptionException()
    {
        // Arrange
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new TransactUpdateBuilder("TestTable")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with encrypted parameter
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Setup mock encryptor to throw exception
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new Exception("Encryption failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FieldEncryptionException>(
            () => builder.EncryptParametersAsync());

        exception.Message.Should().Contain("SensitiveField");
        exception.Message.Should().Contain("Encryption failed");
    }

    [Fact]
    public async Task EncryptParametersAsync_WithNoEncryptedParameters_DoesNothing()
    {
        // Arrange
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new TransactUpdateBuilder("TestTable")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context without encrypted parameters
        var context = new ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            ExpressionValidationMode.None);

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "field")
            .WithValue(":p0", "normal-data");

        // Act
        await builder.EncryptParametersAsync();
        var req = builder.ToWriteItem();

        // Assert
        await mockEncryptor.DidNotReceive().EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify value is not encrypted
        req.Update.ExpressionAttributeValues[":p0"].S.Should().Be("normal-data");
    }

    #endregion Encryption Tests
}
