using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

/// <summary>
/// Tests for SetAt and RemoveAt extension methods in WithUpdateExpressionExtensions.
/// Validates: Requirements 4.4, 4.5
/// </summary>
public class ListIndexUpdateExtensionsTests
{
    #region Test Entity Classes

    /// <summary>
    /// Nested type representing metadata with a list of keywords.
    /// </summary>
    public class Metadata
    {
        [DynamoDbAttribute("keywords")]
        public List<string> Keywords { get; set; } = new();

        [DynamoDbAttribute("scores")]
        public List<int> Scores { get; set; } = new();
    }

    /// <summary>
    /// Test entity with list properties.
    /// </summary>
    private class TestItem : IDynamoDbEntity, IEntityMetadataProvider
    {
        [PartitionKey]
        [DynamoDbAttribute("pk")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute("tags")]
        public List<string> Tags { get; set; } = new();

        [DynamoDbAttribute("scores")]
        public List<int> Scores { get; set; } = new();

        [DynamoDbAttribute("metadata")]
        public Metadata Metadata { get; set; } = new();

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestItem;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Pk ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
        {
            var entity = new TestItem
            {
                Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
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
                TableName = "TestItems",
                Properties = new[]
                {
                    new PropertyMetadata { PropertyName = "Pk", AttributeName = "pk", IsPartitionKey = true },
                    new PropertyMetadata { PropertyName = "Tags", AttributeName = "tags" },
                    new PropertyMetadata { PropertyName = "Scores", AttributeName = "scores" },
                    new PropertyMetadata { PropertyName = "Metadata", AttributeName = "metadata" }
                }
            };
        }

        public static bool RequiresWriteTransaction => false;
    }

    #endregion

    #region Helper Methods

    private UpdateItemRequestBuilder<TestItem> CreateBuilder()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestItem>(client, new FluentDynamoDbOptions());
        
        // Set table and key to make the builder valid
        builder.ForTable("TestItems");
        builder.WithKey("pk", "test-id");
        
        return builder;
    }

    #endregion

    #region SetAt Tests - Top Level List (Requirement 4.4)

    [Fact]
    public void SetAt_TopLevelStringList_ShouldGenerateCorrectSetExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Tags[0], "updated-tag");
        var request = result.ToUpdateItemRequest();

        // Assert - Uses #attr0 placeholder pattern
        request.UpdateExpression.Should().Contain("SET #");
        request.UpdateExpression.Should().Contain("[0] = :v0");
        // Verify the attribute name maps to "tags"
        request.ExpressionAttributeNames.Values.Should().Contain("tags");
        request.ExpressionAttributeValues[":v0"].S.Should().Be("updated-tag");
    }

    [Fact]
    public void SetAt_TopLevelIntList_ShouldGenerateCorrectSetExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Scores[1], 100);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("SET #");
        request.UpdateExpression.Should().Contain("[1] = :v0");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].N.Should().Be("100");
    }

    [Fact]
    public void SetAt_DifferentIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Tags[5], "fifth-tag");
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("[5] = :v0");
    }

    #endregion

    #region SetAt Tests - Nested List (Requirement 4.4)

    [Fact]
    public void SetAt_NestedStringList_ShouldGenerateCorrectSetExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Metadata.Keywords[0], "sale");
        var request = result.ToUpdateItemRequest();

        // Assert - Should have nested path with two attribute names
        request.UpdateExpression.Should().Contain("SET #");
        request.UpdateExpression.Should().Contain(".#");
        request.UpdateExpression.Should().Contain("[0] = :v0");
        request.ExpressionAttributeNames.Values.Should().Contain("metadata");
        request.ExpressionAttributeNames.Values.Should().Contain("keywords");
        request.ExpressionAttributeValues[":v0"].S.Should().Be("sale");
    }

    [Fact]
    public void SetAt_NestedIntList_ShouldGenerateCorrectSetExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Metadata.Scores[2], 95);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("SET #");
        request.UpdateExpression.Should().Contain(".#");
        request.UpdateExpression.Should().Contain("[2] = :v0");
        request.ExpressionAttributeNames.Values.Should().Contain("metadata");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].N.Should().Be("95");
    }

    #endregion

    #region RemoveAt Tests - Top Level List (Requirement 4.5)

    [Fact]
    public void RemoveAt_TopLevelStringList_ShouldGenerateCorrectRemoveExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.RemoveAt(x => x.Tags[2]);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("REMOVE #");
        request.UpdateExpression.Should().Contain("[2]");
        request.ExpressionAttributeNames.Values.Should().Contain("tags");
    }

    [Fact]
    public void RemoveAt_TopLevelIntList_ShouldGenerateCorrectRemoveExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.RemoveAt(x => x.Scores[0]);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("REMOVE #");
        request.UpdateExpression.Should().Contain("[0]");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
    }

    [Fact]
    public void RemoveAt_DifferentIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.RemoveAt(x => x.Tags[10]);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("[10]");
    }

    #endregion

    #region RemoveAt Tests - Nested List (Requirement 4.5)

    [Fact]
    public void RemoveAt_NestedStringList_ShouldGenerateCorrectRemoveExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.RemoveAt(x => x.Metadata.Keywords[1]);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("REMOVE #");
        request.UpdateExpression.Should().Contain(".#");
        request.UpdateExpression.Should().Contain("[1]");
        request.ExpressionAttributeNames.Values.Should().Contain("metadata");
        request.ExpressionAttributeNames.Values.Should().Contain("keywords");
    }

    [Fact]
    public void RemoveAt_NestedIntList_ShouldGenerateCorrectRemoveExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.RemoveAt(x => x.Metadata.Scores[3]);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("REMOVE #");
        request.UpdateExpression.Should().Contain(".#");
        request.UpdateExpression.Should().Contain("[3]");
        request.ExpressionAttributeNames.Values.Should().Contain("metadata");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SetAt_ZeroIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Tags[0], "first");
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("[0] = :v0");
    }

    [Fact]
    public void SetAt_LargeIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.SetAt(x => x.Tags[99], "last");
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("[99] = :v0");
    }

    [Fact]
    public void SetAt_NullValue_ShouldGenerateNullAttributeValue()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        string? nullValue = null;
        var result = builder.SetAt(x => x.Tags[0], nullValue);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("[0] = :v0");
        request.ExpressionAttributeValues[":v0"].NULL.Should().BeTrue();
    }

    #endregion
}
