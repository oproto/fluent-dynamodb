using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class BatchWriteItemBuilderTests
{
    private const string TableName = "TestTable";

    [Fact]
    public void Constructor_ShouldInitializeWithTableName()
    {
        // Act
        var builder = new BatchWriteItemBuilder(TableName);

        // Assert
        builder.Should().NotBeNull();
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().BeEmpty();
    }

    [Fact]
    public void PutItem_WithDictionary_ShouldAddPutRequest()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        var item = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "test-pk" } },
            { "sk", new AttributeValue { S = "test-sk" } },
            { "data", new AttributeValue { S = "test-data" } }
        };

        // Act
        builder.PutItem(item);

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(1);
        writeRequests[0].PutRequest.Should().NotBeNull();
        writeRequests[0].PutRequest.Item.Should().BeEquivalentTo(item);
        writeRequests[0].DeleteRequest.Should().BeNull();
    }

    [Fact]
    public void PutItem_WithMapper_ShouldAddPutRequestWithMappedItem()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        var testObject = new { Id = "test-id", Name = "test-name", Value = 42 };

        Dictionary<string, AttributeValue> Mapper(object obj)
        {
            var testObj = (dynamic)obj;
            return new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = testObj.Id } },
                { "name", new AttributeValue { S = testObj.Name } },
                { "value", new AttributeValue { N = testObj.Value.ToString() } }
            };
        }

        // Act
        builder.PutItem(testObject, Mapper);

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(1);
        writeRequests[0].PutRequest.Should().NotBeNull();
        writeRequests[0].PutRequest.Item.Should().ContainKey("id");
        writeRequests[0].PutRequest.Item["id"].S.Should().Be("test-id");
        writeRequests[0].PutRequest.Item.Should().ContainKey("name");
        writeRequests[0].PutRequest.Item["name"].S.Should().Be("test-name");
        writeRequests[0].PutRequest.Item.Should().ContainKey("value");
        writeRequests[0].PutRequest.Item["value"].N.Should().Be("42");
        writeRequests[0].DeleteRequest.Should().BeNull();
    }

    [Fact]
    public void DeleteItem_WithDictionary_ShouldAddDeleteRequest()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        var key = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "test-pk" } },
            { "sk", new AttributeValue { S = "test-sk" } }
        };

        // Act
        builder.DeleteItem(key);

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(1);
        writeRequests[0].DeleteRequest.Should().NotBeNull();
        writeRequests[0].DeleteRequest.Key.Should().BeEquivalentTo(key);
        writeRequests[0].PutRequest.Should().BeNull();
    }

    [Fact]
    public void DeleteItem_WithSingleKey_ShouldAddDeleteRequest()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        const string keyName = "pk";
        const string keyValue = "test-pk";

        // Act
        builder.DeleteItem(keyName, keyValue);

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(1);
        writeRequests[0].DeleteRequest.Should().NotBeNull();
        writeRequests[0].DeleteRequest.Key.Should().ContainKey(keyName);
        writeRequests[0].DeleteRequest.Key[keyName].S.Should().Be(keyValue);
        writeRequests[0].PutRequest.Should().BeNull();
    }

    [Fact]
    public void DeleteItem_WithCompositeKey_ShouldAddDeleteRequest()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        const string primaryKeyName = "pk";
        const string primaryKeyValue = "test-pk";
        const string sortKeyName = "sk";
        const string sortKeyValue = "test-sk";

        // Act
        builder.DeleteItem(primaryKeyName, primaryKeyValue, sortKeyName, sortKeyValue);

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(1);
        writeRequests[0].DeleteRequest.Should().NotBeNull();
        writeRequests[0].DeleteRequest.Key.Should().ContainKey(primaryKeyName);
        writeRequests[0].DeleteRequest.Key[primaryKeyName].S.Should().Be(primaryKeyValue);
        writeRequests[0].DeleteRequest.Key.Should().ContainKey(sortKeyName);
        writeRequests[0].DeleteRequest.Key[sortKeyName].S.Should().Be(sortKeyValue);
        writeRequests[0].PutRequest.Should().BeNull();
    }

    [Fact]
    public void FluentInterface_ShouldAllowMethodChaining()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        var putItem = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "put-pk" } },
            { "data", new AttributeValue { S = "put-data" } }
        };
        var deleteKey = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "delete-pk" } }
        };

        // Act
        var result = builder
            .PutItem(putItem)
            .DeleteItem(deleteKey)
            .DeleteItem("pk2", "delete-pk2");

        // Assert
        result.Should().BeSameAs(builder);
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(3);

        // First request should be put
        writeRequests[0].PutRequest.Should().NotBeNull();
        writeRequests[0].PutRequest.Item.Should().BeEquivalentTo(putItem);

        // Second request should be delete with dictionary key
        writeRequests[1].DeleteRequest.Should().NotBeNull();
        writeRequests[1].DeleteRequest.Key.Should().BeEquivalentTo(deleteKey);

        // Third request should be delete with string key
        writeRequests[2].DeleteRequest.Should().NotBeNull();
        writeRequests[2].DeleteRequest.Key.Should().ContainKey("pk2");
        writeRequests[2].DeleteRequest.Key["pk2"].S.Should().Be("delete-pk2");
    }

    [Fact]
    public void MixedOperations_ShouldMaintainOrder()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);

        // Act
        builder
            .PutItem(new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "put1" } } })
            .DeleteItem("pk", "delete1")
            .PutItem(new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "put2" } } })
            .DeleteItem("pk", "delete2", "sk", "sort2");

        // Assert
        var writeRequests = builder.ToWriteRequests();
        writeRequests.Should().HaveCount(4);

        // Verify order and types
        writeRequests[0].PutRequest.Should().NotBeNull();
        writeRequests[0].PutRequest.Item["pk"].S.Should().Be("put1");

        writeRequests[1].DeleteRequest.Should().NotBeNull();
        writeRequests[1].DeleteRequest.Key["pk"].S.Should().Be("delete1");

        writeRequests[2].PutRequest.Should().NotBeNull();
        writeRequests[2].PutRequest.Item["pk"].S.Should().Be("put2");

        writeRequests[3].DeleteRequest.Should().NotBeNull();
        writeRequests[3].DeleteRequest.Key["pk"].S.Should().Be("delete2");
        writeRequests[3].DeleteRequest.Key["sk"].S.Should().Be("sort2");
    }

    [Fact]
    public void ToWriteRequests_ShouldReturnCopyOfRequests()
    {
        // Arrange
        var builder = new BatchWriteItemBuilder(TableName);
        builder.PutItem(new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "test" } } });

        // Act
        var requests1 = builder.ToWriteRequests();
        var requests2 = builder.ToWriteRequests();

        // Assert
        requests1.Should().NotBeSameAs(requests2);
        requests1.Should().BeEquivalentTo(requests2);
    }
}