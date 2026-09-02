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
/// Tests for RequireWriteTransaction validation.
/// **Feature: api-enhancements-v0.9, Property 5: Transaction-required entities block non-transactional writes**
/// **Feature: api-enhancements-v0.9, Property 6: Transaction-required entities allow transactional writes**
/// **Validates: Requirements 4.2, 4.3, 4.4, 4.5, 4.6**
/// </summary>
[Collection("OperationContext")]
public class RequireWriteTransactionValidationTests
{
    private readonly IAmazonDynamoDB _mockClient;

    public RequireWriteTransactionValidationTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
    }

    /// <summary>
    /// Test entity that requires write transactions.
    /// </summary>
    public class TransactionRequiredEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        public static bool RequiresWriteTransaction => true;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
        
        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity 
            => new() { ["pk"] = new AttributeValue { S = (entity as TransactionRequiredEntity)?.Id ?? string.Empty } };

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);
        
        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) 
            where TSelf : IReadOnlyEntity 
            => (TSelf)(object)new TransactionRequiredEntity();
        
        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity 
            => (TSelf)(object)new TransactionRequiredEntity();
        
        public static string GetPartitionKey(Dictionary<string, AttributeValue> item) 
            => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        
        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;
        
        public static EntityMetadata GetEntityMetadata() => new() 
        { 
            TableName = "test-table", 
            RequiresWriteTransaction = true, 
            Properties = Array.Empty<PropertyMetadata>(), 
            Indexes = Array.Empty<IndexMetadata>(), 
            Relationships = Array.Empty<RelationshipMetadata>() 
        };
    }


    /// <summary>
    /// Test entity that does NOT require write transactions.
    /// </summary>
    public class NormalEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
        
        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity 
            => new() { ["pk"] = new AttributeValue { S = (entity as NormalEntity)?.Id ?? string.Empty } };

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);
        
        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) 
            where TSelf : IReadOnlyEntity 
            => (TSelf)(object)new NormalEntity();
        
        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) 
            where TSelf : IDynamoDbEntity 
            => (TSelf)(object)new NormalEntity();
        
        public static string GetPartitionKey(Dictionary<string, AttributeValue> item) 
            => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        
        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;
        
        public static EntityMetadata GetEntityMetadata() => new() 
        { 
            TableName = "test-table", 
            RequiresWriteTransaction = false, 
            Properties = Array.Empty<PropertyMetadata>(), 
            Indexes = Array.Empty<IndexMetadata>(), 
            Relationships = Array.Empty<RelationshipMetadata>() 
        };
    }

    #region Property 5: Transaction-required entities block non-transactional writes

    /// <summary>
    /// **Property 5: Transaction-required entities block non-transactional writes**
    /// For any entity with [RequireWriteTransaction], Put operations outside a transaction SHALL throw.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Fact]
    public async Task PutItemRequestBuilder_WithTransactionRequiredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new TransactionRequiredEntity { Id = "test-id", Name = "Test" };
        var builder = new PutItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.ToDynamoDbResponseAsync());
        
        exception.Message.Should().Contain("RequireWriteTransaction");
        exception.Message.Should().Contain("TransactionRequiredEntity");
    }

    /// <summary>
    /// **Property 5: Transaction-required entities block non-transactional writes**
    /// For any entity with [RequireWriteTransaction], Update operations outside a transaction SHALL throw.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public async Task UpdateItemRequestBuilder_WithTransactionRequiredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" })
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.ToDynamoDbResponseAsync());
        
        exception.Message.Should().Contain("RequireWriteTransaction");
        exception.Message.Should().Contain("TransactionRequiredEntity");
    }

    /// <summary>
    /// **Property 5: Transaction-required entities block non-transactional writes**
    /// For any entity with [RequireWriteTransaction], Delete operations outside a transaction SHALL throw.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public async Task DeleteItemRequestBuilder_WithTransactionRequiredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.ToDynamoDbResponseAsync());
        
        exception.Message.Should().Contain("RequireWriteTransaction");
        exception.Message.Should().Contain("TransactionRequiredEntity");
    }


    /// <summary>
    /// **Property 5: Transaction-required entities block non-transactional writes**
    /// For any entity with [RequireWriteTransaction], BatchWrite Put operations SHALL throw.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public void BatchWriteBuilder_AddPut_WithTransactionRequiredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new TransactionRequiredEntity { Id = "test-id", Name = "Test" };
        var putBuilder = new PutItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);
        var batchBuilder = new BatchWriteBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => batchBuilder.Add(putBuilder));
        
        exception.Message.Should().Contain("RequireWriteTransaction");
        exception.Message.Should().Contain("TransactionRequiredEntity");
    }

    /// <summary>
    /// **Property 5: Transaction-required entities block non-transactional writes**
    /// For any entity with [RequireWriteTransaction], BatchWrite Delete operations SHALL throw.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public void BatchWriteBuilder_AddDelete_WithTransactionRequiredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var deleteBuilder = new DeleteItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" });
        var batchBuilder = new BatchWriteBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => batchBuilder.Add(deleteBuilder));
        
        exception.Message.Should().Contain("RequireWriteTransaction");
        exception.Message.Should().Contain("TransactionRequiredEntity");
    }

    #endregion

    #region Property 6: Transaction-required entities allow transactional writes

    /// <summary>
    /// **Property 6: Transaction-required entities allow transactional writes**
    /// For any entity with [RequireWriteTransaction], TransactWrite Put operations SHALL succeed.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Fact]
    public void TransactWriteBuilder_Add_Put_WithTransactionRequiredEntity_DoesNotThrow()
    {
        // Arrange
        var entity = new TransactionRequiredEntity { Id = "test-id", Name = "Test" };
        var putBuilder = new PutItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);
        var transactBuilder = DynamoDbTransactions.Write;

        // Act & Assert
        var act = () => transactBuilder.Add(putBuilder);
        act.Should().NotThrow();
    }

    /// <summary>
    /// **Property 6: Transaction-required entities allow transactional writes**
    /// For any entity with [RequireWriteTransaction], TransactWrite Delete operations SHALL succeed.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Fact]
    public void TransactWriteBuilder_Add_Delete_WithTransactionRequiredEntity_DoesNotThrow()
    {
        // Arrange
        var deleteBuilder = new DeleteItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" });
        var transactBuilder = DynamoDbTransactions.Write;

        // Act & Assert
        var act = () => transactBuilder.Add(deleteBuilder);
        act.Should().NotThrow();
    }

    /// <summary>
    /// **Property 6: Transaction-required entities allow transactional writes**
    /// For any entity with [RequireWriteTransaction], TransactWrite Update operations SHALL succeed.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Fact]
    public void TransactWriteBuilder_Add_Update_WithTransactionRequiredEntity_DoesNotThrow()
    {
        // Arrange
        var updateBuilder = new UpdateItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" })
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated");
        var transactBuilder = DynamoDbTransactions.Write;

        // Act & Assert
        var act = () => transactBuilder.Add(updateBuilder);
        act.Should().NotThrow();
    }

    #endregion


    #region Normal entities (without RequireWriteTransaction) should work normally

    /// <summary>
    /// Verifies that normal entities (without [RequireWriteTransaction]) can be put without transactions.
    /// </summary>
    [Fact]
    public async Task PutItemRequestBuilder_WithNormalEntity_DoesNotThrow()
    {
        // Arrange
        var entity = new NormalEntity { Id = "test-id", Name = "Test" };
        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());
        
        var builder = new PutItemRequestBuilder<NormalEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);

        // Act & Assert - should not throw
        await builder.ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// Verifies that normal entities (without [RequireWriteTransaction]) can be updated without transactions.
    /// </summary>
    [Fact]
    public async Task UpdateItemRequestBuilder_WithNormalEntity_DoesNotThrow()
    {
        // Arrange
        _mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());
        
        var builder = new UpdateItemRequestBuilder<NormalEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" })
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated");

        // Act & Assert - should not throw
        await builder.ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// Verifies that normal entities (without [RequireWriteTransaction]) can be deleted without transactions.
    /// </summary>
    [Fact]
    public async Task DeleteItemRequestBuilder_WithNormalEntity_DoesNotThrow()
    {
        // Arrange
        _mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());
        
        var builder = new DeleteItemRequestBuilder<NormalEntity>(_mockClient)
            .ForTable("test-table")
            .SetKey(k => k["pk"] = new AttributeValue { S = "test-id" });

        // Act & Assert - should not throw
        await builder.ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// Verifies that normal entities (without [RequireWriteTransaction]) can be batch written.
    /// </summary>
    [Fact]
    public async Task BatchWriteBuilder_WithNormalEntity_DoesNotThrow()
    {
        // Arrange
        var entity = new NormalEntity { Id = "test-id", Name = "Test" };
        _mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchWriteItemResponse());
        
        var putBuilder = new PutItemRequestBuilder<NormalEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);
        var batchBuilder = new BatchWriteBuilder().Add(putBuilder);

        // Act & Assert - should not throw
        await batchBuilder.ExecuteAsync();
    }

    #endregion

    #region Error message quality tests

    /// <summary>
    /// Verifies that the error message provides clear guidance on how to fix the issue.
    /// </summary>
    [Fact]
    public async Task ErrorMessage_ContainsGuidanceToUseTransactions()
    {
        // Arrange
        var entity = new TransactionRequiredEntity { Id = "test-id", Name = "Test" };
        var builder = new PutItemRequestBuilder<TransactionRequiredEntity>(_mockClient)
            .ForTable("test-table")
            .WithItem(entity);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.ToDynamoDbResponseAsync());

        // Assert - message should guide user to use transactions
        exception.Message.Should().Contain("DynamoDbTransactions.Write()");
    }

    #endregion
}
