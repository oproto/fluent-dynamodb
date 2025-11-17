using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for marker interface implementations on request builders.
/// These interfaces enable type-safe composition in transaction and batch operations.
/// </summary>
public class MarkerInterfaceTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty },
                ["status"] = new AttributeValue { S = testEntity?.Status ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty,
                Status = item.TryGetValue("status", out var status) ? status.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), logger);
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

    #region PutItemRequestBuilder Marker Interface Tests

    [Fact]
    public void PutItemRequestBuilder_GetTableName_ReturnsCorrectTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        builder.ForTable("TestTable");

        // Act
        var tableName = ((ITransactablePutBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("TestTable");
    }

    [Fact]
    public void PutItemRequestBuilder_GetItem_ReturnsCorrectItemDictionary()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        var entity = new TestEntity { Id = "test-id", Name = "test-name", Status = "active" };
        builder.WithItem(entity, e => TestEntity.ToDynamoDb(e));

        // Act
        var item = ((ITransactablePutBuilder)builder).GetItem();

        // Assert
        item.Should().NotBeNull();
        item.Should().ContainKey("pk");
        item["pk"].S.Should().Be("test-id");
        item.Should().ContainKey("name");
        item["name"].S.Should().Be("test-name");
        item.Should().ContainKey("status");
        item["status"].S.Should().Be("active");
    }

    [Fact]
    public void PutItemRequestBuilder_GetConditionExpression_ReturnsNullWhenNotSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);

        // Act
        var condition = ((ITransactablePutBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().BeNull();
    }

    [Fact]
    public void PutItemRequestBuilder_GetConditionExpression_ReturnsConditionWhenSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        builder.Where("attribute_not_exists(pk)");

        // Act
        var condition = ((ITransactablePutBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("attribute_not_exists(pk)");
    }

    [Fact]
    public void PutItemRequestBuilder_GetExpressionAttributeNames_ReturnsNullWhenEmpty()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);

        // Act
        var names = ((ITransactablePutBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().BeNull();
    }

    [Fact]
    public void PutItemRequestBuilder_GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        builder.WithAttribute("#name", "name");
        builder.WithAttribute("#status", "status");

        // Act
        var names = ((ITransactablePutBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#name");
        names!["#name"].Should().Be("name");
        names.Should().ContainKey("#status");
        names["#status"].Should().Be("status");
    }

    [Fact]
    public void PutItemRequestBuilder_GetExpressionAttributeValues_ReturnsNullWhenEmpty()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);

        // Act
        var values = ((ITransactablePutBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().BeNull();
    }

    [Fact]
    public void PutItemRequestBuilder_GetExpressionAttributeValues_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        builder.WithValue(":name", "John");
        builder.WithValue(":status", "active");

        // Act
        var values = ((ITransactablePutBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().NotBeNull();
        values.Should().ContainKey(":name");
        values![":name"].S.Should().Be("John");
        values.Should().ContainKey(":status");
        values[":status"].S.Should().Be("active");
    }

    [Fact]
    public void PutItemRequestBuilder_WithStringFormattingPlaceholders_ExtractsCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(client);
        builder.Where("attribute_not_exists({0})", "pk");

        // Act
        var condition = ((ITransactablePutBuilder)builder).GetConditionExpression();
        var values = ((ITransactablePutBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().NotBeNull();
        condition.Should().Contain(":p");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("pk");
    }

    #endregion

    #region UpdateItemRequestBuilder Marker Interface Tests

    [Fact]
    public void UpdateItemRequestBuilder_GetTableName_ReturnsCorrectTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.ForTable("TestTable");

        // Act
        var tableName = ((ITransactableUpdateBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("TestTable");
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetKey_ReturnsCorrectKey()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.WithKey("pk", "test-id", "sk", "test-sk");

        // Act
        var key = ((ITransactableUpdateBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
        key.Should().ContainKey("sk");
        key["sk"].S.Should().Be("test-sk");
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetUpdateExpression_ReturnsCorrectExpression()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.Set("SET #name = :name");

        // Act
        var expression = ((ITransactableUpdateBuilder)builder).GetUpdateExpression();

        // Assert
        expression.Should().Be("SET #name = :name");
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetConditionExpression_ReturnsNullWhenNotSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);

        // Act
        var condition = ((ITransactableUpdateBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().BeNull();
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetConditionExpression_ReturnsConditionWhenSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.Where("attribute_exists(pk)");

        // Act
        var condition = ((ITransactableUpdateBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.WithAttribute("#name", "name");

        // Act
        var names = ((ITransactableUpdateBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#name");
        names!["#name"].Should().Be("name");
    }

    [Fact]
    public void UpdateItemRequestBuilder_GetExpressionAttributeValues_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.WithValue(":name", "John");

        // Act
        var values = ((ITransactableUpdateBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().NotBeNull();
        values.Should().ContainKey(":name");
        values![":name"].S.Should().Be("John");
    }

    [Fact]
    public void UpdateItemRequestBuilder_WithStringFormattingPlaceholders_ExtractsCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(client);
        builder.Set("SET #name = {0}", "NewName");

        // Act
        var expression = ((ITransactableUpdateBuilder)builder).GetUpdateExpression();
        var values = ((ITransactableUpdateBuilder)builder).GetExpressionAttributeValues();

        // Assert
        expression.Should().NotBeNull();
        expression.Should().Contain(":p");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("NewName");
    }

    #endregion

    #region DeleteItemRequestBuilder Marker Interface Tests

    [Fact]
    public void DeleteItemRequestBuilder_GetTableName_ReturnsCorrectTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.ForTable("TestTable");

        // Act
        var tableName = ((ITransactableDeleteBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("TestTable");
    }

    [Fact]
    public void DeleteItemRequestBuilder_GetKey_ReturnsCorrectKey()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.WithKey("pk", "test-id");

        // Act
        var key = ((ITransactableDeleteBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
    }

    [Fact]
    public void DeleteItemRequestBuilder_GetConditionExpression_ReturnsNullWhenNotSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);

        // Act
        var condition = ((ITransactableDeleteBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().BeNull();
    }

    [Fact]
    public void DeleteItemRequestBuilder_GetConditionExpression_ReturnsConditionWhenSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.Where("attribute_exists(pk)");

        // Act
        var condition = ((ITransactableDeleteBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void DeleteItemRequestBuilder_GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.WithAttribute("#pk", "pk");

        // Act
        var names = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#pk");
        names!["#pk"].Should().Be("pk");
    }

    [Fact]
    public void DeleteItemRequestBuilder_GetExpressionAttributeValues_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.WithValue(":status", "deleted");

        // Act
        var values = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().NotBeNull();
        values.Should().ContainKey(":status");
        values![":status"].S.Should().Be("deleted");
    }

    [Fact]
    public void DeleteItemRequestBuilder_WithConditionAndPlaceholders_ExtractsCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(client);
        builder.Where("#status = {0}", "active")
               .WithAttribute("#status", "status");

        // Act
        var condition = ((ITransactableDeleteBuilder)builder).GetConditionExpression();
        var names = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeNames();
        var values = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().NotBeNull();
        names.Should().NotBeNull();
        names.Should().ContainKey("#status");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("active");
    }

    #endregion

    #region GetItemRequestBuilder Marker Interface Tests

    [Fact]
    public void GetItemRequestBuilder_GetTableName_ReturnsCorrectTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.ForTable("TestTable");

        // Act
        var tableName = ((ITransactableGetBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("TestTable");
    }

    [Fact]
    public void GetItemRequestBuilder_GetKey_ReturnsCorrectKey()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.WithKey("pk", "test-id");

        // Act
        var key = ((ITransactableGetBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
    }

    [Fact]
    public void GetItemRequestBuilder_GetProjectionExpression_ReturnsNullWhenNotSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);

        // Act
        var projection = ((ITransactableGetBuilder)builder).GetProjectionExpression();

        // Assert
        projection.Should().BeNull();
    }

    [Fact]
    public void GetItemRequestBuilder_GetProjectionExpression_ReturnsProjectionWhenSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.WithProjection("pk, name, status");

        // Act
        var projection = ((ITransactableGetBuilder)builder).GetProjectionExpression();

        // Assert
        projection.Should().Be("pk, name, status");
    }

    [Fact]
    public void GetItemRequestBuilder_GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.WithAttribute("#name", "name");

        // Act
        var names = ((ITransactableGetBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#name");
        names!["#name"].Should().Be("name");
    }

    [Fact]
    public void GetItemRequestBuilder_GetConsistentRead_ReturnsFalseByDefault()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);

        // Act
        var consistentRead = ((ITransactableGetBuilder)builder).GetConsistentRead();

        // Assert
        consistentRead.Should().BeFalse();
    }

    [Fact]
    public void GetItemRequestBuilder_GetConsistentRead_ReturnsTrueWhenSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.UsingConsistentRead();

        // Act
        var consistentRead = ((ITransactableGetBuilder)builder).GetConsistentRead();

        // Assert
        consistentRead.Should().BeTrue();
    }

    [Fact]
    public void GetItemRequestBuilder_WithProjectionAndAttributeNames_ExtractsCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(client);
        builder.WithProjection("#name, #status");
        builder.WithAttribute("#name", "name");
        builder.WithAttribute("#status", "status");

        // Act
        var projection = ((ITransactableGetBuilder)builder).GetProjectionExpression();
        var names = ((ITransactableGetBuilder)builder).GetExpressionAttributeNames();

        // Assert
        projection.Should().Be("#name, #status");
        names.Should().NotBeNull();
        names.Should().ContainKey("#name");
        names!["#name"].Should().Be("name");
        names.Should().ContainKey("#status");
        names["#status"].Should().Be("status");
    }

    #endregion

    #region ConditionCheckBuilder Marker Interface Tests

    [Fact]
    public void ConditionCheckBuilder_GetTableName_ReturnsCorrectTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        var tableName = ((ITransactableConditionCheckBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("TestTable");
    }

    [Fact]
    public void ConditionCheckBuilder_GetKey_ReturnsCorrectKey()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.WithKey("pk", "test-id");

        // Act
        var key = ((ITransactableConditionCheckBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
    }

    [Fact]
    public void ConditionCheckBuilder_GetConditionExpression_ReturnsCondition()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.Where("attribute_exists(pk)");

        // Act
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void ConditionCheckBuilder_GetConditionExpression_ThrowsWhenNotSet()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act & Assert
        var act = () => ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Condition expression is required*");
    }

    [Fact]
    public void ConditionCheckBuilder_GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        // Directly access the helper since ConditionCheckBuilder doesn't implement IWithAttributeNames
        builder.GetAttributeNameHelper().WithAttribute("#status", "status");

        // Act
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#status");
        names!["#status"].Should().Be("status");
    }

    [Fact]
    public void ConditionCheckBuilder_GetExpressionAttributeValues_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        // Directly access the helper since ConditionCheckBuilder doesn't implement IWithAttributeValues
        builder.GetAttributeValueHelper().WithValue(":status", "active");

        // Act
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().NotBeNull();
        values.Should().ContainKey(":status");
        values![":status"].S.Should().Be("active");
    }

    [Fact]
    public void ConditionCheckBuilder_WithStringFormattingPlaceholders_ExtractsCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.Where("#status = {0}", "active");

        // Act
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().NotBeNull();
        names.Should().BeNull(); // #status is not added automatically, only :p0 is added
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("active");
    }

    [Fact]
    public void ConditionCheckBuilder_DoesNotExposeExecuteAsync()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act & Assert
        var builderType = builder.GetType();
        var executeMethod = builderType.GetMethod("ExecuteAsync");
        executeMethod.Should().BeNull("ConditionCheckBuilder should not expose ExecuteAsync method");
    }

    #endregion
}
