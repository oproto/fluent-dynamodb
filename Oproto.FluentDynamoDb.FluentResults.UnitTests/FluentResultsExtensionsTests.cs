using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentResults;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.FluentResults.UnitTests;

public class FluentResultsExtensionsTests
{
    private readonly IAmazonDynamoDB _mockClient;

    public FluentResultsExtensionsTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
    }

    [Fact]
    public async Task ExecuteAsyncResult_GetItem_Success_ReturnsOkResult()
    {
        // Arrange
        var builder = new GetItemRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var mockResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-pk" },
                ["name"] = new AttributeValue { S = "test-name" }
            }
        };

        _mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.GetItemAsyncResult();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncResult_GetItem_Exception_ReturnsFailResult()
    {
        // Arrange
        var builder = new GetItemRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var exception = new Exception("Test exception");

        _mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetItemResponse>(exception));

        // Act
        var result = await builder.GetItemAsyncResult();

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Errors[0].Message.Should().Contain("Failed to execute GetItem operation for TestEntity");
    }

    [Fact]
    public async Task ToListAsyncResult_Query_Success_ReturnsOkResult()
    {
        // Arrange
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["pk"] = new AttributeValue { S = "test-pk" },
                    ["name"] = new AttributeValue { S = "test-name" }
                }
            },
            Count = 1
        };

        _mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ToListAsyncResult<TestEntity>();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToListAsyncResult_Query_Exception_ReturnsFailResult()
    {
        // Arrange
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var exception = new Exception("Test exception");

        _mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<QueryResponse>(exception));

        // Act
        var result = await builder.ToListAsyncResult<TestEntity>();

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Errors[0].Message.Should().Contain("Failed to execute Query operation for TestEntity");
    }

    [Fact]
    public async Task ToListAsyncResult_Scan_Success_ReturnsOkResult()
    {
        // Arrange
        var builder = new ScanRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new()
                {
                    ["pk"] = new AttributeValue { S = "test-pk" },
                    ["name"] = new AttributeValue { S = "test-name" }
                }
            },
            Count = 1
        };

        _mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ToListAsyncResult<TestEntity>();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToListAsyncResult_Scan_Exception_ReturnsFailResult()
    {
        // Arrange
        var builder = new ScanRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var exception = new Exception("Test exception");

        _mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScanResponse>(exception));

        // Act
        var result = await builder.ToListAsyncResult<TestEntity>();

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Errors[0].Message.Should().Contain("Failed to execute Scan operation for TestEntity");
    }


    [Fact]
    public async Task PutAsyncResult_PutItem_Success_ReturnsOkResult()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        var mockResponse = new PutItemResponse();

        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.WithItem(entity).PutAsyncResult();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PutAsyncResult_PutItem_Exception_ReturnsFailResult()
    {
        // Arrange
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        var exception = new Exception("Test exception");

        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PutItemResponse>(exception));

        // Act
        var result = await builder.WithItem(entity).PutAsyncResult();

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
        result.Errors[0].Message.Should().Contain("Failed to execute PutItem operation");
    }

    // Test removed - GetDynamoDbItemsResult method was removed in Task 41
    // The method was a leftover from the old multi-item design that was replaced by ToListAsync/ToCompositeEntityAsync

    [Fact]
    public async Task ExecuteAsyncResult_OperationCanceled_RethrowsException()
    {
        // Arrange
        var builder = new GetItemRequestBuilder<TestEntity>(_mockClient).ForTable("test-table");
        var cancellationToken = new CancellationToken(true);

        _mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<GetItemResponse>(cancellationToken));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => builder.GetItemAsyncResult(cancellationToken));
    }
}

// Test entity for unit tests
public partial class TestEntity : IDynamoDbEntity
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
        return item.ContainsKey("pk") && item.ContainsKey("name");
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