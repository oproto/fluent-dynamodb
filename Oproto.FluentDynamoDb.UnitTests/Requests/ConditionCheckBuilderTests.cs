using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Tests for ConditionCheckBuilder functionality.
/// Verifies builder configuration, marker interface implementation, and integration with transactions.
/// </summary>
public class ConditionCheckBuilderTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty },
                ["status"] = new AttributeValue { S = testEntity?.Status ?? string.Empty },
                ["count"] = new AttributeValue { N = testEntity?.Count.ToString() ?? "0" }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty,
                Status = item.TryGetValue("status", out var status) ? status.S : string.Empty,
                Count = item.TryGetValue("count", out var count) && int.TryParse(count.N, out var c) ? c : 0
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
                Properties = new[]
                {
                    new PropertyMetadata
                    {
                        PropertyName = "Id",
                        AttributeName = "pk",
                        IsPartitionKey = true
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "Status",
                        AttributeName = "status"
                    },
                    new PropertyMetadata
                    {
                        PropertyName = "Count",
                        AttributeName = "count"
                    }
                },
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }
    }

    #region Builder Configuration Tests (21.1)

    [Fact]
    public void WithKey_SingleKey_SetsKeyCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.WithKey("pk", "test-id");
        var key = ((ITransactableConditionCheckBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
    }

    [Fact]
    public void WithKey_CompositeKey_SetsKeysCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.WithKey("pk", "test-id", "sk", "test-sk");
        var key = ((ITransactableConditionCheckBuilder)builder).GetKey();

        // Assert
        key.Should().NotBeNull();
        key.Should().ContainKey("pk");
        key["pk"].S.Should().Be("test-id");
        key.Should().ContainKey("sk");
        key["sk"].S.Should().Be("test-sk");
    }

    [Fact]
    public void Where_SimpleCondition_SetsConditionExpression()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.Where("attribute_exists(pk)");
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void Where_MultipleConditions_CombinesWithAnd()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.Where("attribute_exists(pk)");
        builder.Where("#status = :status");
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();

        // Assert
        condition.Should().Be("(attribute_exists(pk)) AND (#status = :status)");
    }

    [Fact]
    public void Where_WithStringFormattingPlaceholder_ProcessesCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.Where("#status = {0}", "active");
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().NotBeNull();
        condition.Should().Contain(":p0");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("active");
    }

    [Fact]
    public void Where_WithMultiplePlaceholders_ProcessesAllCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.Where("#status = {0} AND #count > {1}", "active", 10);
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().NotBeNull();
        condition.Should().Contain(":p0");
        condition.Should().Contain(":p1");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("active");
        values.Should().ContainKey(":p1");
        values[":p1"].N.Should().Be("10");
    }

    [Fact]
    public void Where_WithAttributeNamesAndPlaceholders_CombinesCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        builder.Where("#status = {0}", "active");
        builder.GetAttributeNameHelper().WithAttribute("#status", "status");
        
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        condition.Should().Contain("#status");
        condition.Should().Contain(":p0");
        names.Should().NotBeNull();
        names.Should().ContainKey("#status");
        names!["#status"].Should().Be("status");
        values.Should().NotBeNull();
        values.Should().ContainKey(":p0");
        values![":p0"].S.Should().Be("active");
    }

    [Fact]
    public void ForTable_ChangesTableName()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "OriginalTable");

        // Act
        builder.ForTable("NewTable");
        var tableName = ((ITransactableConditionCheckBuilder)builder).GetTableName();

        // Assert
        tableName.Should().Be("NewTable");
    }

    [Fact]
    public void Self_ReturnsBuilderInstance()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        var self = builder.Self;

        // Assert
        self.Should().BeSameAs(builder);
    }

    #endregion

    #region Marker Interface Implementation Tests (21.2)

    [Fact]
    public void GetTableName_ReturnsCorrectTableName()
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
    public void GetKey_ReturnsConfiguredKey()
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
    public void GetConditionExpression_ReturnsConditionWhenSet()
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
    public void GetConditionExpression_ThrowsWhenNotSet()
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
    public void GetExpressionAttributeNames_ReturnsNullWhenEmpty()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().BeNull();
    }

    [Fact]
    public void GetExpressionAttributeNames_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.GetAttributeNameHelper().WithAttribute("#status", "status");
        builder.GetAttributeNameHelper().WithAttribute("#count", "count");

        // Act
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();

        // Assert
        names.Should().NotBeNull();
        names.Should().ContainKey("#status");
        names!["#status"].Should().Be("status");
        names.Should().ContainKey("#count");
        names["#count"].Should().Be("count");
    }

    [Fact]
    public void GetExpressionAttributeValues_ReturnsNullWhenEmpty()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().BeNull();
    }

    [Fact]
    public void GetExpressionAttributeValues_ReturnsCorrectMappings()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.GetAttributeValueHelper().WithValue(":status", "active");
        builder.GetAttributeValueHelper().WithValue(":count", 10);

        // Act
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        // Assert
        values.Should().NotBeNull();
        values.Should().ContainKey(":status");
        values![":status"].S.Should().Be("active");
        values.Should().ContainKey(":count");
        values[":count"].N.Should().Be("10");
    }

    [Fact]
    public void MarkerInterface_AllMethodsReturnCorrectValues()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.WithKey("pk", "test-id", "sk", "test-sk");
        builder.Where("#status = :status");
        builder.GetAttributeNameHelper().WithAttribute("#status", "status");
        builder.GetAttributeValueHelper().WithValue(":status", "active");

        var markerInterface = (ITransactableConditionCheckBuilder)builder;

        // Act & Assert
        markerInterface.GetTableName().Should().Be("TestTable");
        markerInterface.GetKey().Should().ContainKey("pk");
        markerInterface.GetKey()["pk"].S.Should().Be("test-id");
        markerInterface.GetKey().Should().ContainKey("sk");
        markerInterface.GetKey()["sk"].S.Should().Be("test-sk");
        markerInterface.GetConditionExpression().Should().Be("#status = :status");
        markerInterface.GetExpressionAttributeNames().Should().ContainKey("#status");
        markerInterface.GetExpressionAttributeNames()!["#status"].Should().Be("status");
        markerInterface.GetExpressionAttributeValues().Should().ContainKey(":status");
        markerInterface.GetExpressionAttributeValues()![":status"].S.Should().Be("active");
    }

    #endregion

    #region ExecuteAsync Not Exposed Tests

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

    [Fact]
    public void ConditionCheckBuilder_DoesNotExposeToDynamoDbResponseAsync()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");

        // Act & Assert
        var builderType = builder.GetType();
        var toResponseMethod = builderType.GetMethod("ToDynamoDbResponseAsync");
        toResponseMethod.Should().BeNull("ConditionCheckBuilder should not expose ToDynamoDbResponseAsync method");
    }

    #endregion

    #region Integration with Transaction Tests

    [Fact]
    public void ConditionCheckBuilder_CanBeAddedToTransaction()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.WithKey("pk", "test-id");
        builder.Where("attribute_exists(pk)");

        // Act
        var transaction = DynamoDbTransactions.Write.Add(builder);

        // Assert
        transaction.Should().NotBeNull();
    }

    [Fact]
    public void ConditionCheckBuilder_WithComplexCondition_WorksInTransaction()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable");
        builder.WithKey("pk", "test-id");
        builder.Where("#status = {0} AND #count > {1}", "active", 5);
        builder.GetAttributeNameHelper().WithAttribute("#status", "status");
        builder.GetAttributeNameHelper().WithAttribute("#count", "count");

        // Act
        var transaction = DynamoDbTransactions.Write.Add(builder);

        // Assert
        transaction.Should().NotBeNull();
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        condition.Should().Contain(":p0");
        condition.Should().Contain(":p1");
    }

    #endregion

    #region Method Chaining Tests

    [Fact]
    public void ConditionCheckBuilder_SupportsMethodChaining()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();

        // Act
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable")
            .WithKey("pk", "test-id")
            .Where("attribute_exists(pk)");

        // Assert
        builder.Should().NotBeNull();
        var key = ((ITransactableConditionCheckBuilder)builder).GetKey();
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        
        key.Should().ContainKey("pk");
        condition.Should().Be("attribute_exists(pk)");
    }

    [Fact]
    public void ConditionCheckBuilder_ComplexChaining_WorksCorrectly()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();

        // Act
        var builder = new ConditionCheckBuilder<TestEntity>(client, "TestTable")
            .WithKey("pk", "test-id", "sk", "test-sk")
            .Where("#status = {0}", "active")
            .Where("#count > {0}", 10);

        builder.GetAttributeNameHelper().WithAttribute("#status", "status");
        builder.GetAttributeNameHelper().WithAttribute("#count", "count");

        // Assert
        var condition = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression();
        var names = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames();
        var values = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues();

        condition.Should().Contain("AND");
        names.Should().ContainKey("#status");
        names.Should().ContainKey("#count");
        values.Should().ContainKey(":p0");
        values.Should().ContainKey(":p1");
    }

    #endregion
}
