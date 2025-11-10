using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactWriteItemsRequestBuilderTests
{
    #region Encryption Tests

    [Fact]
    public async Task ExecuteAsync_WithEncryptedUpdateParameter_EncryptsBeforeExecution()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        var mockTable = Substitute.For<DynamoDbTableBase>(mockClient, "TestTable", null, mockEncryptor);
        
        var builder = new TransactWriteItemsRequestBuilder(mockClient);

        // Setup mock encryptor to return encrypted bytes
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        var mockResponse = new TransactWriteItemsResponse();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        builder.Update(mockTable, update =>
        {
            // Create expression context with encrypted parameter
            var context = new ExpressionContext(
                update.GetAttributeValueHelper(),
                update.GetAttributeNameHelper(),
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

            update.SetExpressionContext(context);
            update.WithKey("pk", "test-id")
                .Set("SET #field = :p0")
                .WithAttribute("#field", "sensitive_field")
                .WithValue(":p0", "sensitive-data");
        });

        await builder.ExecuteAsync();

        // Assert
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "SensitiveField",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify the transaction was sent with encrypted value (as binary)
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Update != null &&
                req.TransactItems[0].Update.ExpressionAttributeValues[":p0"].B != null &&
                req.TransactItems[0].Update.ExpressionAttributeValues[":p0"].B.ToArray().SequenceEqual(encryptedBytes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleEncryptedUpdates_EncryptsAll()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        var mockTable = Substitute.For<DynamoDbTableBase>(mockClient, "TestTable", null, mockEncryptor);
        
        var builder = new TransactWriteItemsRequestBuilder(mockClient);

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

        var mockResponse = new TransactWriteItemsResponse();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Add two update operations with encrypted parameters
        builder.Update(mockTable, update =>
        {
            var context = new ExpressionContext(
                update.GetAttributeValueHelper(),
                update.GetAttributeNameHelper(),
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

            update.SetExpressionContext(context);
            update.WithKey("pk", "test-id-1")
                .Set("SET #field = :p0")
                .WithAttribute("#field", "field1")
                .WithValue(":p0", "sensitive-data-1");
        });

        builder.Update(mockTable, update =>
        {
            var context = new ExpressionContext(
                update.GetAttributeValueHelper(),
                update.GetAttributeNameHelper(),
                null,
                ExpressionValidationMode.None);

            context.ParameterMetadata.Add(new ParameterMetadata
            {
                ParameterName = ":p0",
                Value = new AttributeValue { S = "sensitive-data-2" },
                RequiresEncryption = true,
                PropertyName = "Field2",
                AttributeName = "field2"
            });

            update.SetExpressionContext(context);
            update.WithKey("pk", "test-id-2")
                .Set("SET #field = :p0")
                .WithAttribute("#field", "field2")
                .WithValue(":p0", "sensitive-data-2");
        });

        await builder.ExecuteAsync();

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

        // Verify both updates have encrypted values (as binary)
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 2 &&
                req.TransactItems[0].Update.ExpressionAttributeValues[":p0"].B != null &&
                req.TransactItems[0].Update.ExpressionAttributeValues[":p0"].B.ToArray().SequenceEqual(encryptedBytes1) &&
                req.TransactItems[1].Update.ExpressionAttributeValues[":p0"].B != null &&
                req.TransactItems[1].Update.ExpressionAttributeValues[":p0"].B.ToArray().SequenceEqual(encryptedBytes2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedOperations_OnlyEncryptsUpdates()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        var mockTable = Substitute.For<DynamoDbTableBase>(mockClient, "TestTable", null, mockEncryptor);
        
        var builder = new TransactWriteItemsRequestBuilder(mockClient);

        // Setup mock encryptor
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        var mockResponse = new TransactWriteItemsResponse();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Add Put, Update (with encryption), and Delete operations
        builder.Put(mockTable, put =>
        {
            put.WithItem(new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "put-id" } },
                { "data", new AttributeValue { S = "put-data" } }
            });
        });

        builder.Update(mockTable, update =>
        {
            var context = new ExpressionContext(
                update.GetAttributeValueHelper(),
                update.GetAttributeNameHelper(),
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

            update.SetExpressionContext(context);
            update.WithKey("pk", "update-id")
                .Set("SET #field = :p0")
                .WithAttribute("#field", "sensitive_field")
                .WithValue(":p0", "sensitive-data");
        });

        builder.Delete(mockTable, delete =>
        {
            delete.WithKey("pk", "delete-id");
        });

        await builder.ExecuteAsync();

        // Assert - Only the update operation should trigger encryption
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "SensitiveField",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify transaction has all three operations with encrypted value (as binary)
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 3 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[1].Update != null &&
                req.TransactItems[1].Update.ExpressionAttributeValues[":p0"].B != null &&
                req.TransactItems[1].Update.ExpressionAttributeValues[":p0"].B.ToArray().SequenceEqual(encryptedBytes) &&
                req.TransactItems[2].Delete != null),
            Arg.Any<CancellationToken>());
    }

    #endregion Encryption Tests
}
