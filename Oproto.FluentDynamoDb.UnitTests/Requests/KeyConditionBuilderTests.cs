using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for KeyCondition enum and builder methods (IfExists, IfNotExists, WithKeyCondition).
/// </summary>
public class KeyConditionBuilderTests
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

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

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

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

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

    #region KeyCondition Enum Tests

    [Fact]
    public void KeyCondition_None_HasValueZero()
    {
        ((int)KeyCondition.None).Should().Be(0);
    }

    [Fact]
    public void KeyCondition_MustExist_HasValueOne()
    {
        ((int)KeyCondition.MustExist).Should().Be(1);
    }

    [Fact]
    public void KeyCondition_MustNotExist_HasValueTwo()
    {
        ((int)KeyCondition.MustNotExist).Should().Be(2);
    }

    [Fact]
    public void KeyCondition_DefaultValue_IsNone()
    {
        KeyCondition defaultValue = default;
        defaultValue.Should().Be(KeyCondition.None);
    }

    #endregion

    #region PutItemRequestBuilder Tests

    [Fact]
    public void PutBuilder_IfExists_SetsKeyConditionToMustExist_SimpleKey()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void PutBuilder_IfNotExists_SetsKeyConditionToMustNotExist_SimpleKey()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk)");
    }

    [Fact]
    public void PutBuilder_IfExists_SetsKeyConditionToMustExist_CompositeKey()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk) AND attribute_exists(sk)");
    }

    [Fact]
    public void PutBuilder_IfNotExists_SetsKeyConditionToMustNotExist_CompositeKey()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk) AND attribute_not_exists(sk)");
    }

    [Fact]
    public void PutBuilder_WithKeyCondition_MustExist_EquivalentToIfExists()
    {
        // Arrange
        var builder1 = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        var builder2 = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder1.ForTable("test-table");
        builder2.ForTable("test-table");

        // Act
        builder1.IfExists();
        builder2.WithKeyCondition(KeyCondition.MustExist);
        var request1 = builder1.ToPutItemRequest();
        var request2 = builder2.ToPutItemRequest();

        // Assert
        request1.ConditionExpression.Should().Be(request2.ConditionExpression);
    }

    [Fact]
    public void PutBuilder_WithKeyCondition_MustNotExist_EquivalentToIfNotExists()
    {
        // Arrange
        var builder1 = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        var builder2 = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder1.ForTable("test-table");
        builder2.ForTable("test-table");

        // Act
        builder1.IfNotExists();
        builder2.WithKeyCondition(KeyCondition.MustNotExist);
        var request1 = builder1.ToPutItemRequest();
        var request2 = builder2.ToPutItemRequest();

        // Assert
        request1.ConditionExpression.Should().Be(request2.ConditionExpression);
    }

    [Fact]
    public void PutBuilder_WithKeyCondition_None_DoesNotAddCondition()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.WithKeyCondition(KeyCondition.None);
        var request = builder.ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().BeNull();
    }

    [Fact]
    public void PutBuilder_MethodChaining_ReturnsBuilder()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());

        // Act & Assert - verify method chaining works
        builder.ForTable("test-table")
            .IfExists()
            .ReturnAllOldValues()
            .Should().BeSameAs(builder);
    }

    [Fact]
    public void PutBuilder_KeyCondition_CombinesWithExistingWhereClause()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act - Set a condition expression first, then add key condition
        builder.SetConditionExpression("#version = :version");
        builder.IfExists();
        var request = builder.ToPutItemRequest();

        // Assert - Key condition should be prepended
        request.ConditionExpression.Should().Be("(attribute_exists(pk)) AND (#version = :version)");
    }

    #endregion

    #region UpdateItemRequestBuilder Tests

    [Fact]
    public void UpdateBuilder_IfExists_SetsKeyConditionToMustExist_SimpleKey()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void UpdateBuilder_IfNotExists_SetsKeyConditionToMustNotExist_SimpleKey()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk)");
    }

    [Fact]
    public void UpdateBuilder_IfExists_SetsKeyConditionToMustExist_CompositeKey()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk) AND attribute_exists(sk)");
    }

    [Fact]
    public void UpdateBuilder_IfNotExists_SetsKeyConditionToMustNotExist_CompositeKey()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk) AND attribute_not_exists(sk)");
    }

    [Fact]
    public void UpdateBuilder_WithKeyCondition_MustExist_EquivalentToIfExists()
    {
        // Arrange
        var builder1 = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        var builder2 = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder1.ForTable("test-table");
        builder2.ForTable("test-table");

        // Act
        builder1.IfExists();
        builder2.WithKeyCondition(KeyCondition.MustExist);
        var request1 = builder1.ToUpdateItemRequest();
        var request2 = builder2.ToUpdateItemRequest();

        // Assert
        request1.ConditionExpression.Should().Be(request2.ConditionExpression);
    }

    [Fact]
    public void UpdateBuilder_WithKeyCondition_None_DoesNotAddCondition()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.WithKeyCondition(KeyCondition.None);
        var request = builder.ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().BeNull();
    }

    [Fact]
    public void UpdateBuilder_MethodChaining_ReturnsBuilder()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());

        // Act & Assert - verify method chaining works
        builder.ForTable("test-table")
            .IfExists()
            .ReturnAllNewValues()
            .Should().BeSameAs(builder);
    }

    [Fact]
    public void UpdateBuilder_KeyCondition_CombinesWithExistingWhereClause()
    {
        // Arrange
        var builder = new UpdateItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act - Set a Where clause first, then add key condition
        builder.SetConditionExpression("#version = :version");
        builder.IfExists();
        var request = builder.ToUpdateItemRequest();

        // Assert - Key condition should be prepended
        request.ConditionExpression.Should().Be("(attribute_exists(pk)) AND (#version = :version)");
    }

    #endregion

    #region DeleteItemRequestBuilder Tests

    [Fact]
    public void DeleteBuilder_IfExists_SetsKeyConditionToMustExist_SimpleKey()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void DeleteBuilder_IfNotExists_SetsKeyConditionToMustNotExist_SimpleKey()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk)");
    }

    [Fact]
    public void DeleteBuilder_IfExists_SetsKeyConditionToMustExist_CompositeKey()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfExists();
        var request = builder.ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_exists(pk) AND attribute_exists(sk)");
    }

    [Fact]
    public void DeleteBuilder_IfNotExists_SetsKeyConditionToMustNotExist_CompositeKey()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<CompositeKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.IfNotExists();
        var request = builder.ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("attribute_not_exists(pk) AND attribute_not_exists(sk)");
    }

    [Fact]
    public void DeleteBuilder_WithKeyCondition_MustExist_EquivalentToIfExists()
    {
        // Arrange
        var builder1 = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        var builder2 = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder1.ForTable("test-table");
        builder2.ForTable("test-table");

        // Act
        builder1.IfExists();
        builder2.WithKeyCondition(KeyCondition.MustExist);
        var request1 = builder1.ToDeleteItemRequest();
        var request2 = builder2.ToDeleteItemRequest();

        // Assert
        request1.ConditionExpression.Should().Be(request2.ConditionExpression);
    }

    [Fact]
    public void DeleteBuilder_WithKeyCondition_None_DoesNotAddCondition()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act
        builder.WithKeyCondition(KeyCondition.None);
        var request = builder.ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().BeNull();
    }

    [Fact]
    public void DeleteBuilder_MethodChaining_ReturnsBuilder()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());

        // Act & Assert - verify method chaining works
        builder.ForTable("test-table")
            .IfExists()
            .ReturnAllOldValues()
            .Should().BeSameAs(builder);
    }

    [Fact]
    public void DeleteBuilder_KeyCondition_CombinesWithExistingWhereClause()
    {
        // Arrange
        var builder = new DeleteItemRequestBuilder<SimpleKeyEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("test-table");

        // Act - Set a Where clause first, then add key condition
        builder.SetConditionExpression("#status = :status");
        builder.IfExists();
        var request = builder.ToDeleteItemRequest();

        // Assert - Key condition should be prepended
        request.ConditionExpression.Should().Be("(attribute_exists(pk)) AND (#status = :status)");
    }

    #endregion
}
