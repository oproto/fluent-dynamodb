using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for KeyCondition compatibility with transactions and batch operations.
/// Verifies that key conditions are preserved when builders are added to transactions,
/// and that batch operations correctly ignore conditions (as per DynamoDB limitations).
/// </summary>
public class KeyConditionTransactionBatchTests
{
    #region Test Entities

    /// <summary>
    /// Test entity with only a partition key (simple key).
    /// </summary>
    private class SimpleKeyEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as SimpleKeyEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
        {
            var entity = new SimpleKeyEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
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

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => item.ContainsKey("pk");

        public static EntityMetadata GetEntityMetadata() => new EntityMetadata
        {
            TableName = "simple-key-table",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = null,
            SortKeyAttributeType = null
        };

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    /// <summary>
    /// Test entity with partition key and sort key (composite key).
    /// </summary>
    private class CompositeKeyEntity : IDynamoDbEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as CompositeKeyEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Pk ?? string.Empty },
                ["sk"] = new AttributeValue { S = testEntity?.Sk ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
        {
            var entity = new CompositeKeyEntity
            {
                Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Sk = item.TryGetValue("sk", out var sk) ? sk.S : string.Empty
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

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => item.ContainsKey("pk") && item.ContainsKey("sk");

        public static EntityMetadata GetEntityMetadata() => new EntityMetadata
        {
            TableName = "composite-key-table",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = "sk",
            SortKeyAttributeType = "S"
        };

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    #endregion

    #region Transaction Tests - Put with Key Condition

    [Fact]
    public async Task Transaction_PutWithIfExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "attribute_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_PutWithIfNotExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .IfNotExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "attribute_not_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_PutWithIfExists_PreservesCondition_CompositeKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<CompositeKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new CompositeKeyEntity { Pk = "pk-value", Sk = "sk-value" })
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "attribute_exists(pk) AND attribute_exists(sk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_PutWithIfNotExists_PreservesCondition_CompositeKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<CompositeKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new CompositeKeyEntity { Pk = "pk-value", Sk = "sk-value" })
            .IfNotExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "attribute_not_exists(pk) AND attribute_not_exists(sk)"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Transaction Tests - Update with Key Condition

    [Fact]
    public async Task Transaction_UpdateWithIfExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var updateBuilder = new UpdateItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "new-name")
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(updateBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Update != null &&
                req.TransactItems[0].Update.ConditionExpression == "attribute_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_UpdateWithIfNotExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var updateBuilder = new UpdateItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "new-name")
            .IfNotExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(updateBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Update != null &&
                req.TransactItems[0].Update.ConditionExpression == "attribute_not_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_UpdateWithIfExists_PreservesCondition_CompositeKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var updateBuilder = new UpdateItemRequestBuilder<CompositeKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "pk-value", "sk", "sk-value")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "new-name")
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(updateBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Update != null &&
                req.TransactItems[0].Update.ConditionExpression == "attribute_exists(pk) AND attribute_exists(sk)"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Transaction Tests - Delete with Key Condition

    [Fact]
    public async Task Transaction_DeleteWithIfExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "test-id")
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Delete != null &&
                req.TransactItems[0].Delete.ConditionExpression == "attribute_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_DeleteWithIfNotExists_PreservesCondition_SimpleKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "test-id")
            .IfNotExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Delete != null &&
                req.TransactItems[0].Delete.ConditionExpression == "attribute_not_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_DeleteWithIfExists_PreservesCondition_CompositeKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var deleteBuilder = new DeleteItemRequestBuilder<CompositeKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "pk-value", "sk", "sk-value")
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Delete != null &&
                req.TransactItems[0].Delete.ConditionExpression == "attribute_exists(pk) AND attribute_exists(sk)"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Transaction Tests - Combined Key Condition with Where Clause

    [Fact]
    public async Task Transaction_PutWithKeyConditionAndWhereClause_PreservesCombinedCondition()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" });
        
        // Set a Where clause first, then add key condition
        putBuilder.SetConditionExpression("#version = :version");
        putBuilder.IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert - Key condition should be prepended to existing condition
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 1 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "(attribute_exists(pk)) AND (#version = :version)"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Transaction Tests - Multiple Operations with Key Conditions

    [Fact]
    public async Task Transaction_MultipleOperationsWithKeyConditions_PreservesAllConditions()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var putBuilder = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "put-id" })
            .IfNotExists();

        var updateBuilder = new UpdateItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "update-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "updated")
            .IfExists();

        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "delete-id")
            .IfExists();

        // Act
        await DynamoDbTransactions.Write
            .Add(putBuilder)
            .Add(updateBuilder)
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(req =>
                req.TransactItems.Count == 3 &&
                req.TransactItems[0].Put != null &&
                req.TransactItems[0].Put.ConditionExpression == "attribute_not_exists(pk)" &&
                req.TransactItems[1].Update != null &&
                req.TransactItems[1].Update.ConditionExpression == "attribute_exists(pk)" &&
                req.TransactItems[2].Delete != null &&
                req.TransactItems[2].Delete.ConditionExpression == "attribute_exists(pk)"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Batch Tests - Conditions Are Ignored (DynamoDB Limitation)

    /// <summary>
    /// Verifies that batch operations correctly ignore key conditions.
    /// DynamoDB BatchWriteItem does not support condition expressions.
    /// </summary>
    [Fact]
    public async Task Batch_PutWithKeyCondition_ConditionIsIgnored()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var putBuilder = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .IfNotExists(); // This condition should be ignored in batch

        // Act
        await DynamoDbBatch.Write
            .Add(putBuilder)
            .ExecuteAsync();

        // Assert - BatchWriteItem doesn't have ConditionExpression
        // The request should succeed without the condition
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems.ContainsKey("test-table") &&
                req.RequestItems["test-table"].Count == 1 &&
                req.RequestItems["test-table"][0].PutRequest != null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that batch delete operations correctly ignore key conditions.
    /// DynamoDB BatchWriteItem does not support condition expressions.
    /// </summary>
    [Fact]
    public async Task Batch_DeleteWithKeyCondition_ConditionIsIgnored()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());

        var deleteBuilder = new DeleteItemRequestBuilder<SimpleKeyEntity>(mockClient)
            .ForTable("test-table")
            .WithKey("pk", "test-id")
            .IfExists(); // This condition should be ignored in batch

        // Act
        await DynamoDbBatch.Write
            .Add(deleteBuilder)
            .ExecuteAsync();

        // Assert - BatchWriteItem doesn't have ConditionExpression
        // The request should succeed without the condition
        await mockClient.Received(1).BatchWriteItemAsync(
            Arg.Is<BatchWriteItemRequest>(req =>
                req.RequestItems.ContainsKey("test-table") &&
                req.RequestItems["test-table"].Count == 1 &&
                req.RequestItems["test-table"][0].DeleteRequest != null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Builder Method Equivalence in Transactions

    [Fact]
    public async Task Transaction_IfExistsEquivalentToWithKeyConditionMustExist()
    {
        // Arrange
        var mockClient1 = Substitute.For<IAmazonDynamoDB>();
        var mockClient2 = Substitute.For<IAmazonDynamoDB>();
        
        TransactWriteItemsRequest? capturedRequest1 = null;
        TransactWriteItemsRequest? capturedRequest2 = null;
        
        mockClient1.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest1 = callInfo.Arg<TransactWriteItemsRequest>();
                return new TransactWriteItemsResponse();
            });
        
        mockClient2.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest2 = callInfo.Arg<TransactWriteItemsRequest>();
                return new TransactWriteItemsResponse();
            });

        var putBuilder1 = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient1)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .IfExists();

        var putBuilder2 = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient2)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .WithKeyCondition(KeyCondition.MustExist);

        // Act
        await DynamoDbTransactions.Write.Add(putBuilder1).ExecuteAsync();
        await DynamoDbTransactions.Write.Add(putBuilder2).ExecuteAsync();

        // Assert
        capturedRequest1.Should().NotBeNull();
        capturedRequest2.Should().NotBeNull();
        capturedRequest1!.TransactItems[0].Put!.ConditionExpression
            .Should().Be(capturedRequest2!.TransactItems[0].Put!.ConditionExpression);
    }

    [Fact]
    public async Task Transaction_IfNotExistsEquivalentToWithKeyConditionMustNotExist()
    {
        // Arrange
        var mockClient1 = Substitute.For<IAmazonDynamoDB>();
        var mockClient2 = Substitute.For<IAmazonDynamoDB>();
        
        TransactWriteItemsRequest? capturedRequest1 = null;
        TransactWriteItemsRequest? capturedRequest2 = null;
        
        mockClient1.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest1 = callInfo.Arg<TransactWriteItemsRequest>();
                return new TransactWriteItemsResponse();
            });
        
        mockClient2.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest2 = callInfo.Arg<TransactWriteItemsRequest>();
                return new TransactWriteItemsResponse();
            });

        var putBuilder1 = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient1)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .IfNotExists();

        var putBuilder2 = new PutItemRequestBuilder<SimpleKeyEntity>(mockClient2)
            .ForTable("test-table")
            .WithItem(new SimpleKeyEntity { Id = "test-id" })
            .WithKeyCondition(KeyCondition.MustNotExist);

        // Act
        await DynamoDbTransactions.Write.Add(putBuilder1).ExecuteAsync();
        await DynamoDbTransactions.Write.Add(putBuilder2).ExecuteAsync();

        // Assert
        capturedRequest1.Should().NotBeNull();
        capturedRequest2.Should().NotBeNull();
        capturedRequest1!.TransactItems[0].Put!.ConditionExpression
            .Should().Be(capturedRequest2!.TransactItems[0].Put!.ConditionExpression);
    }

    #endregion
}
