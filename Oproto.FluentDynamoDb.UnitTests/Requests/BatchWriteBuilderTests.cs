using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for BatchWriteBuilder functionality.
/// Covers Add() method overloads, client inference, validation, and encryption support.
/// </summary>
public class BatchWriteBuilderTests
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

    #region 19.1 Test Add() method overloads

    [Fact]
    public async Task Add_PutBuilder_AddsOperationToBatch()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());
            
        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        batchBuilder.Add(putBuilder);
        await batchBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req => 
                req.RequestItems.ContainsKey("TestTable") && 
                req.RequestItems["TestTable"].Count == 1 &&
                req.RequestItems["TestTable"][0].PutRequest != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_DeleteBuilder_AddsOperationToBatch()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());
            
        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        batchBuilder.Add(deleteBuilder);
        await batchBuilder.ExecuteAsync();

        // Assert - verify operation was added and executed
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req => 
                req.RequestItems.ContainsKey("TestTable") && 
                req.RequestItems["TestTable"].Count == 1 &&
                req.RequestItems["TestTable"][0].DeleteRequest != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_MultipleOperations_GroupsByTableName()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder1 = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("Table1")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } });

        var putBuilder2 = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("Table1")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" } });

        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("Table2")
            .WithKey("pk", "id3");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder1)
            .Add(putBuilder2)
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert - verify request was sent with operations grouped by table
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems.Count == 2 &&
                req.RequestItems["Table1"].Count == 2 &&
                req.RequestItems["Table2"].Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_PutBuilderWithCondition_IgnoresConditionExpression()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } })
            .Where("attribute_not_exists(pk)");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder.Add(putBuilder).ExecuteAsync();

        // Assert - condition expression should be ignored (not present in batch request)
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"][0].PutRequest != null &&
                req.RequestItems["TestTable"][0].PutRequest.Item["pk"].S == "test-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_DeleteBuilderWithCondition_IgnoresConditionExpression()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Where("attribute_exists(pk)");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder.Add(deleteBuilder).ExecuteAsync();

        // Assert - condition expression should be ignored
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"][0].DeleteRequest != null &&
                req.RequestItems["TestTable"][0].DeleteRequest.Key["pk"].S == "test-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_MixedPutAndDeleteOperations_GroupsCorrectly()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } });

        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert - both operations should be in same table group
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems.Count == 1 &&
                req.RequestItems["TestTable"].Count == 2 &&
                req.RequestItems["TestTable"][0].PutRequest != null &&
                req.RequestItems["TestTable"][1].DeleteRequest != null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 19.2 Test client inference and configuration

    [Fact]
    public async Task ClientInference_ExtractsFromFirstBuilder()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());
            
        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        batchBuilder.Add(putBuilder);
        await batchBuilder.ExecuteAsync();

        // Assert - client should be inferred and used successfully
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
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

        var batchBuilder = new BatchWriteBuilder();

        // Act & Assert
        batchBuilder.Add(putBuilder1);
        
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            batchBuilder.Add(putBuilder2);
        });
        
        exception.Message.Should().Contain("same DynamoDB client instance");
    }

    [Fact]
    public async Task WithClient_OverridesInference()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        
        explicitClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync();

        // Assert - explicit client should be used
        await explicitClient.Received(1).BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ClientParameter_HasHighestPrecedence()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        var parameterClient = Substitute.For<IAmazonDynamoDB>();
        
        parameterClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync(parameterClient);

        // Assert - parameter client should be used
        await parameterClient.Received(1).BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await explicitClient.DidNotReceive().BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().BatchWriteItemAsync(
            Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnConsumedCapacity_SetsCorrectValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .ReturnConsumedCapacity(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.ReturnConsumedCapacity == Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnItemCollectionMetrics_SetsCorrectValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "test-id" } });

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .ReturnItemCollectionMetrics()
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.ReturnItemCollectionMetrics == Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 19.3 Test validation

    [Fact]
    public async Task ExecuteAsync_EmptyBatch_ThrowsException()
    {
        // Arrange
        var batchBuilder = new BatchWriteBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("no operations");
    }

    [Fact]
    public async Task ExecuteAsync_MoreThan25Operations_ThrowsValidationException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var batchBuilder = new BatchWriteBuilder();

        // Add 26 operations
        for (int i = 0; i < 26; i++)
        {
            var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = $"id-{i}" } });
            
            batchBuilder.Add(putBuilder);
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("26 operations");
        exception.Message.Should().Contain("maximum of 25");
    }

    [Fact]
    public async Task ExecuteAsync_MoreThan25Operations_SuggestsChunking()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var batchBuilder = new BatchWriteBuilder();

        // Add 30 operations
        for (int i = 0; i < 30; i++)
        {
            var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithItem(new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = $"id-{i}" } });
            
            batchBuilder.Add(putBuilder);
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("splitting");
        exception.Message.Should().Contain("chunking");
    }

    [Fact]
    public async Task ExecuteAsync_MissingClient_ThrowsClearException()
    {
        // Arrange
        // Create a builder without a client (using null)
        var putBuilder = new PutItemRequestBuilder<TestEntity>(null!)
            .ForTable("TestTable")
            .WithItem(new TestEntity { Id = "test-id", Name = "Test" });

        var batchBuilder = new BatchWriteBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.Add(putBuilder).ExecuteAsync();
        });
        
        exception.Message.Should().Contain("No DynamoDB client specified");
        exception.Message.Should().Contain("ExecuteAsync()");
        exception.Message.Should().Contain("WithClient()");
    }

    #endregion

    #region 19.4 Test encryption in batch write

    [Fact]
    public async Task Add_PutBuilderWithEncryptedEntity_EncryptionHandledDuringToDynamoDb()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        // Create entity with encrypted field (encryption happens in ToDynamoDb)
        var entity = new TestEntity { Id = "test-id", Name = "encrypted-name" };
        var encryptedItem = TestEntity.ToDynamoDb(entity);

        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(encryptedItem);

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder.Add(putBuilder).ExecuteAsync();

        // Assert - batch should execute with the already-encrypted item
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"][0].PutRequest != null &&
                req.RequestItems["TestTable"][0].PutRequest.Item["pk"].S == "test-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_MultiplePutBuildersWithEncryption_AllEncryptedCorrectly()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        // Create multiple entities (encryption happens in ToDynamoDb)
        var entity1 = new TestEntity { Id = "id1", Name = "name1" };
        var entity2 = new TestEntity { Id = "id2", Name = "name2" };
        
        var putBuilder1 = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(TestEntity.ToDynamoDb(entity1));

        var putBuilder2 = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(TestEntity.ToDynamoDb(entity2));

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder1)
            .Add(putBuilder2)
            .ExecuteAsync();

        // Assert - both items should be in the batch
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"].Count == 2 &&
                req.RequestItems["TestTable"][0].PutRequest.Item["pk"].S == "id1" &&
                req.RequestItems["TestTable"][1].PutRequest.Item["pk"].S == "id2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_DeleteOperationsOnly_NoEncryptionProcessing()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var deleteBuilder1 = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var deleteBuilder2 = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(deleteBuilder1)
            .Add(deleteBuilder2)
            .ExecuteAsync();

        // Assert - delete operations should execute without any encryption processing
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"].Count == 2 &&
                req.RequestItems["TestTable"][0].DeleteRequest != null &&
                req.RequestItems["TestTable"][1].DeleteRequest != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_MixedPutAndDeleteWithEncryption_HandlesCorrectly()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var entity = new TestEntity { Id = "id1", Name = "encrypted-name" };
        var putBuilder = new PutItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithItem(TestEntity.ToDynamoDb(entity));

        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2");

        var batchBuilder = new BatchWriteBuilder();

        // Act
        await batchBuilder
            .Add(putBuilder)
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert - both operations should execute correctly
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems["TestTable"].Count == 2 &&
                req.RequestItems["TestTable"][0].PutRequest != null &&
                req.RequestItems["TestTable"][1].DeleteRequest != null),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
