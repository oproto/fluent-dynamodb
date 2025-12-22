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
/// Tests for AddToSet and DeleteFromSet extension methods in WithUpdateExpressionExtensions.
/// Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5
/// </summary>
public class SetOperationExtensionsTests
{
    #region Test Entity Classes

    /// <summary>
    /// Test entity with set properties.
    /// </summary>
    private class TestItem : IDynamoDbEntity, IEntityMetadataProvider
    {
        [PartitionKey]
        [DynamoDbAttribute("pk")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute("categories")]
        public HashSet<string> Categories { get; set; } = new();

        [DynamoDbAttribute("scores")]
        public HashSet<int> Scores { get; set; } = new();

        [DynamoDbAttribute("prices")]
        public HashSet<decimal> Prices { get; set; } = new();

        [DynamoDbAttribute("ratings")]
        public HashSet<double> Ratings { get; set; } = new();

        [DynamoDbAttribute("counts")]
        public HashSet<long> Counts { get; set; } = new();

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
                    new PropertyMetadata { PropertyName = "Categories", AttributeName = "categories" },
                    new PropertyMetadata { PropertyName = "Scores", AttributeName = "scores" },
                    new PropertyMetadata { PropertyName = "Prices", AttributeName = "prices" },
                    new PropertyMetadata { PropertyName = "Ratings", AttributeName = "ratings" },
                    new PropertyMetadata { PropertyName = "Counts", AttributeName = "counts" }
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

    #region AddToSet Tests - Single Element (Requirement 5.1)

    [Fact]
    public void AddToSet_SingleStringElement_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Categories, "electronics");
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("categories");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("electronics");
    }

    [Fact]
    public void AddToSet_SingleIntElement_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Scores, 100);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("100");
    }

    [Fact]
    public void AddToSet_SingleDecimalElement_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Prices, 99.99m);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.ExpressionAttributeNames.Values.Should().Contain("prices");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("99.99");
    }

    [Fact]
    public void AddToSet_SingleDoubleElement_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Ratings, 4.5);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.ExpressionAttributeNames.Values.Should().Contain("ratings");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("4.5");
    }

    [Fact]
    public void AddToSet_SingleLongElement_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Counts, 1000000L);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.ExpressionAttributeNames.Values.Should().Contain("counts");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("1000000");
    }

    #endregion

    #region AddToSet Tests - Multiple Elements (Requirement 5.2)

    [Fact]
    public void AddToSet_MultipleStringElements_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Categories, new[] { "electronics", "sale" });
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("categories");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("electronics");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("sale");
    }

    [Fact]
    public void AddToSet_MultipleIntElements_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Scores, new[] { 100, 200, 300 });
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("100");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("200");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("300");
    }

    [Fact]
    public void AddToSet_MultipleDecimalElements_ShouldGenerateCorrectAddExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Prices, new[] { 9.99m, 19.99m });
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("ADD #");
        request.ExpressionAttributeNames.Values.Should().Contain("prices");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("9.99");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("19.99");
    }

    #endregion

    #region DeleteFromSet Tests - Single Element (Requirement 5.3)

    [Fact]
    public void DeleteFromSet_SingleStringElement_ShouldGenerateCorrectDeleteExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Categories, "clearance");
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("DELETE #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("categories");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("clearance");
    }

    [Fact]
    public void DeleteFromSet_SingleIntElement_ShouldGenerateCorrectDeleteExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Scores, 50);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("DELETE #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("50");
    }

    [Fact]
    public void DeleteFromSet_SingleDecimalElement_ShouldGenerateCorrectDeleteExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Prices, 9.99m);
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("DELETE #");
        request.ExpressionAttributeNames.Values.Should().Contain("prices");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("9.99");
    }

    #endregion

    #region DeleteFromSet Tests - Multiple Elements (Requirement 5.4)

    [Fact]
    public void DeleteFromSet_MultipleStringElements_ShouldGenerateCorrectDeleteExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Categories, new[] { "clearance", "discontinued" });
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("DELETE #");
        request.UpdateExpression.Should().Contain(":v0");
        request.ExpressionAttributeNames.Values.Should().Contain("categories");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("clearance");
        request.ExpressionAttributeValues[":v0"].SS.Should().Contain("discontinued");
    }

    [Fact]
    public void DeleteFromSet_MultipleIntElements_ShouldGenerateCorrectDeleteExpression()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Scores, new[] { 50, 75 });
        var request = result.ToUpdateItemRequest();

        // Assert
        request.UpdateExpression.Should().Contain("DELETE #");
        request.ExpressionAttributeNames.Values.Should().Contain("scores");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("50");
        request.ExpressionAttributeValues[":v0"].NS.Should().Contain("75");
    }

    #endregion

    #region Numeric Set Tests (Requirement 5.5)

    [Fact]
    public void AddToSet_NumericSet_ShouldGenerateNumberSetAttributeValue()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.AddToSet(x => x.Scores, 100);
        var request = result.ToUpdateItemRequest();

        // Assert - Should be NS (Number Set), not SS (String Set)
        request.ExpressionAttributeValues[":v0"].NS.Should().NotBeNull();
        request.ExpressionAttributeValues[":v0"].SS.Should().BeNullOrEmpty();
    }

    [Fact]
    public void DeleteFromSet_NumericSet_ShouldGenerateNumberSetAttributeValue()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var result = builder.DeleteFromSet(x => x.Scores, 50);
        var request = result.ToUpdateItemRequest();

        // Assert - Should be NS (Number Set), not SS (String Set)
        request.ExpressionAttributeValues[":v0"].NS.Should().NotBeNull();
        request.ExpressionAttributeValues[":v0"].SS.Should().BeNullOrEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AddToSet_EmptyArray_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.AddToSet(x => x.Categories, Array.Empty<string>());
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Cannot create a set with zero elements*");
    }

    [Fact]
    public void DeleteFromSet_EmptyArray_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.DeleteFromSet(x => x.Categories, Array.Empty<string>());
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Cannot create a set with zero elements*");
    }

    [Fact]
    public void AddToSet_NullSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.AddToSet<TestItem, string>(null!, "value");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeleteFromSet_NullSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.DeleteFromSet<TestItem, string>(null!, "value");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddToSet_NullValues_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.AddToSet(x => x.Categories, (IEnumerable<string>)null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeleteFromSet_NullValues_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act & Assert
        var action = () => builder.DeleteFromSet(x => x.Categories, (IEnumerable<string>)null!);
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
