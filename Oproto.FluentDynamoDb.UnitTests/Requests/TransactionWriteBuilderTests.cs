using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactionWriteBuilderTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }
    }

    #region 17.1 Test Add() method overloads

    [Fact]
    public async Task Add_PutBuilder_AddsOperationToTransaction()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
            
        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        transactionBuilder.Add(putBuilder);
        await transactionBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req => req.TransactItems.Count == 1 && req.TransactItems[0].Put != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_UpdateBuilder_AddsOperationToTransaction()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
            
        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "new-name");

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        transactionBuilder.Add(updateBuilder);
        await transactionBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req => req.TransactItems.Count == 1 && req.TransactItems[0].Update != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_DeleteBuilder_AddsOperationToTransaction()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
            
        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        transactionBuilder.Add(deleteBuilder);
        await transactionBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req => req.TransactItems.Count == 1 && req.TransactItems[0].Delete != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_ConditionCheckBuilder_AddsOperationToTransaction()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
            
        var conditionCheckBuilder = new ConditionCheckBuilder<TestEntity>(mockClient, "TestTable")
            .WithKey("pk", "test-id")
            .Where("attribute_exists(pk)");

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        transactionBuilder.Add(conditionCheckBuilder);
        await transactionBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req => req.TransactItems.Count == 1 && req.TransactItems[0].ConditionCheck != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_MultipleOperations_MaintainsOrder()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } });

        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "updated");

        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id3");

        var conditionCheckBuilder = new ConditionCheckBuilder<TestEntity>(mockClient, "TestTable")
            .WithKey("pk", "id4")
            .Where("attribute_exists(pk)");

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .Add(updateBuilder)
            .Add(deleteBuilder)
            .Add(conditionCheckBuilder)
            .ExecuteAsync();

        // Assert - verify request was sent with operations in correct order
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 4 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[1].Update != null &&
                req.TransactItems[2].Delete != null &&
                req.TransactItems[3].ConditionCheck != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_PutBuilder_ExtractsAllSettings()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } })
            .Where("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "test-id");

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder.Add(putBuilder).ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems[0].Put.TableName == "TestTable" &&
                req.TransactItems[0].Put.Item["pk"].S == "test-id" &&
                req.TransactItems[0].Put.ConditionExpression == "#pk = :pk" &&
                req.TransactItems[0].Put.ExpressionAttributeNames["#pk"] == "pk" &&
                req.TransactItems[0].Put.ExpressionAttributeValues[":pk"].S == "test-id"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 17.2 Test client inference

    [Fact]
    public async Task ClientInference_ExtractsFromFirstBuilder()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
            
        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        transactionBuilder.Add(putBuilder);
        await transactionBuilder.ExecuteAsync();

        // Assert - client should be inferred and used successfully
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClientInference_DetectsMismatch_ThrowsException()
    {
        // Arrange
        var mockClient1 = Substitute.For<IAmazonDynamoDB>();
        var mockClient2 = Substitute.For<IAmazonDynamoDB>();
        
        var putBuilder1 = new PutItemRequestBuilder<TestEntity>(mockClient1)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } });

        var putBuilder2 = new PutItemRequestBuilder<TestEntity>(mockClient2)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act & Assert
        transactionBuilder.Add(putBuilder1);
        
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            transactionBuilder.Add(putBuilder2);
        });
        
        exception.Message.Should().Contain("same DynamoDB client instance");
    }

    [Fact]
    public async Task WithClient_OverridesInference()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        
        explicitClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync();

        // Assert - explicit client should be used
        await explicitClient.Received(1).TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ClientParameter_HasHighestPrecedence()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        var parameterClient = Substitute.For<IAmazonDynamoDB>();
        
        parameterClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync(parameterClient);

        // Assert - parameter client should be used
        await parameterClient.Received(1).TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
        
        await explicitClient.DidNotReceive().TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().TransactWriteItemsAsync(
            Arg.Any<TransactWriteItemsRequest>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 17.3 Test transaction-level configuration

    [Fact]
    public async Task ReturnConsumedCapacity_SetsCorrectValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .ReturnConsumedCapacity(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.ReturnConsumedCapacity == Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithClientRequestToken_SetsToken()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();
        var token = Guid.NewGuid().ToString();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .WithClientRequestToken(token)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.ClientRequestToken == token),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnItemCollectionMetrics_SetsCorrectValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(putBuilder)
            .ReturnItemCollectionMetrics()
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.ReturnItemCollectionMetrics == Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 17.4 Test validation

    [Fact]
    public async Task ExecuteAsync_EmptyTransaction_ThrowsException()
    {
        // Arrange
        var transactionBuilder = new TransactionWriteBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await transactionBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("no operations");
    }

    [Fact]
    public async Task ExecuteAsync_MoreThan100Operations_ThrowsException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var transactionBuilder = new TransactionWriteBuilder();

        // Add 101 operations
        for (int i = 0; i < 101; i++)
        {
            var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = $"id-{i}" } });
            
            transactionBuilder.Add(putBuilder);
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await transactionBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("101 operations");
        exception.Message.Should().Contain("maximum of 100");
    }

    [Fact]
    public async Task ExecuteAsync_MissingClient_ThrowsClearException()
    {
        // Arrange
        // Create a builder without a client (using null)
        var putBuilder = new PutItemRequestBuilder<TestEntity>(null!)
            .ForTable("TestTable")
            .WithItem(new TestEntity { Id = "test-id", Name = "Test" });

        var transactionBuilder = new TransactionWriteBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await transactionBuilder.Add(putBuilder).ExecuteAsync();
        });
        
        exception.Message.Should().Contain("No DynamoDB client specified");
        exception.Message.Should().Contain("ExecuteAsync()");
        exception.Message.Should().Contain("WithClient()");
    }

    #endregion

    #region 17.5 Test encryption in transactions

    [Fact]
    public async Task ExecuteAsync_WithEncryptedFields_EncryptsBeforeExecution()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with encrypted parameter
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            updateBuilder.GetAttributeValueHelper(),
            updateBuilder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        updateBuilder.SetExpressionContext(context);

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(updateBuilder)
            .ExecuteAsync();

        // Assert - encryption should have been called
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "SensitiveField",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EncryptionException_ThrowsWithContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");
        // Note: No encryptor set

        // Create expression context with encrypted parameter but no encryptor
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            updateBuilder.GetAttributeValueHelper(),
            updateBuilder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        updateBuilder.SetExpressionContext(context);

        var transactionBuilder = new TransactionWriteBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await transactionBuilder
                .Add(updateBuilder)
                .ExecuteAsync();
        });
        
        exception.Message.Should().Contain("Transaction execution failed");
        exception.Message.Should().Contain("IFieldEncryptor");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleUpdatesWithEncryption_EncryptsAll()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        // Create first update builder with encryption
        var updateBuilder1 = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1")
            .Set("SET #field = :p0")
            .WithAttribute("#field", "field1")
            .WithValue(":p0", "data1")
            .SetFieldEncryptor(mockEncryptor);

        var context1 = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            updateBuilder1.GetAttributeValueHelper(),
            updateBuilder1.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context1.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "data1" },
            RequiresEncryption = true,
            PropertyName = "Field1",
            AttributeName = "field1"
        });

        updateBuilder1.SetExpressionContext(context1);

        // Create second update builder with encryption
        var updateBuilder2 = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2")
            .Set("SET #field = :p0")
            .WithAttribute("#field", "field2")
            .WithValue(":p0", "data2")
            .SetFieldEncryptor(mockEncryptor);

        var context2 = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            updateBuilder2.GetAttributeValueHelper(),
            updateBuilder2.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context2.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "data2" },
            RequiresEncryption = true,
            PropertyName = "Field2",
            AttributeName = "field2"
        });

        updateBuilder2.SetExpressionContext(context2);

        var transactionBuilder = new TransactionWriteBuilder();

        // Act
        await transactionBuilder
            .Add(updateBuilder1)
            .Add(updateBuilder2)
            .ExecuteAsync();

        // Assert - encryption should have been called for both updates
        await mockEncryptor.Received(2).EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
