using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

public class DynamoDbOperationContextTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
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

    [Fact]
    public void Clear_RemovesCurrentContext()
    {
        // Arrange
        DynamoDbOperationContext.Current = new OperationContextData
        {
            OperationType = "Query",
            TableName = "TestTable"
        };

        // Act
        DynamoDbOperationContext.Clear();

        // Assert
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    [Fact]
    public void EncryptionContextId_GetSet_WorksCorrectly()
    {
        // Arrange
        DynamoDbOperationContext.Clear();

        // Act
        DynamoDbOperationContext.EncryptionContextId = "test-context-id";

        // Assert
        DynamoDbOperationContext.EncryptionContextId.Should().Be("test-context-id");
        DynamoDbOperationContext.Current.Should().NotBeNull();
        DynamoDbOperationContext.Current!.EncryptionContextId.Should().Be("test-context-id");
    }

    #region DeserializeRawItem Tests

    [Fact]
    public void DeserializeRawItem_WithValidItem_ReturnsEntity()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItem = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-id" },
                ["name"] = new AttributeValue { S = "test-name" }
            }
        };

        // Act
        var result = contextData.DeserializeRawItem<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-id");
        result.Name.Should().Be("test-name");
    }

    [Fact]
    public void DeserializeRawItem_WithNullRawItem_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItem = null
        };

        // Act
        var result = contextData.DeserializeRawItem<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DeserializeRawItem_WithNonMatchingItem_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItem = new Dictionary<string, AttributeValue>
            {
                ["other_field"] = new AttributeValue { S = "value" }
            }
        };

        // Act
        var result = contextData.DeserializeRawItem<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    #endregion DeserializeRawItem Tests

    #region DeserializeRawItems Tests

    [Fact]
    public void DeserializeRawItems_WithValidItems_ReturnsEntityList()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItems = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
                new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
            }
        };

        // Act
        var result = contextData.DeserializeRawItems<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("id1");
        result[0].Name.Should().Be("name1");
        result[1].Id.Should().Be("id2");
        result[1].Name.Should().Be("name2");
    }

    [Fact]
    public void DeserializeRawItems_WithNullRawItems_ReturnsEmptyList()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItems = null
        };

        // Act
        var result = contextData.DeserializeRawItems<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeRawItems_WithEmptyRawItems_ReturnsEmptyList()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItems = new List<Dictionary<string, AttributeValue>>()
        };

        // Act
        var result = contextData.DeserializeRawItems<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeRawItems_WithMixedMatchingItems_ReturnsOnlyMatchingEntities()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            RawItems = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
                new() { ["other_field"] = new AttributeValue { S = "value" } }, // Non-matching
                new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
            }
        };

        // Act
        var result = contextData.DeserializeRawItems<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("id1");
        result[1].Id.Should().Be("id2");
    }

    #endregion DeserializeRawItems Tests

    #region DeserializePreOperationValue Tests

    [Fact]
    public void DeserializePreOperationValue_WithValidAttributes_ReturnsEntity()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PreOperationValues = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "old-id" },
                ["name"] = new AttributeValue { S = "old-name" }
            }
        };

        // Act
        var result = contextData.DeserializePreOperationValue<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("old-id");
        result.Name.Should().Be("old-name");
    }

    [Fact]
    public void DeserializePreOperationValue_WithNullPreOperationValues_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PreOperationValues = null
        };

        // Act
        var result = contextData.DeserializePreOperationValue<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DeserializePreOperationValue_WithNonMatchingAttributes_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PreOperationValues = new Dictionary<string, AttributeValue>
            {
                ["other_field"] = new AttributeValue { S = "value" }
            }
        };

        // Act
        var result = contextData.DeserializePreOperationValue<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    #endregion DeserializePreOperationValue Tests

    #region DeserializePostOperationValue Tests

    [Fact]
    public void DeserializePostOperationValue_WithValidAttributes_ReturnsEntity()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PostOperationValues = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "new-id" },
                ["name"] = new AttributeValue { S = "new-name" }
            }
        };

        // Act
        var result = contextData.DeserializePostOperationValue<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("new-id");
        result.Name.Should().Be("new-name");
    }

    [Fact]
    public void DeserializePostOperationValue_WithNullPostOperationValues_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PostOperationValues = null
        };

        // Act
        var result = contextData.DeserializePostOperationValue<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DeserializePostOperationValue_WithNonMatchingAttributes_ReturnsNull()
    {
        // Arrange
        var contextData = new OperationContextData
        {
            PostOperationValues = new Dictionary<string, AttributeValue>
            {
                ["other_field"] = new AttributeValue { S = "value" }
            }
        };

        // Act
        var result = contextData.DeserializePostOperationValue<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    #endregion DeserializePostOperationValue Tests
}
