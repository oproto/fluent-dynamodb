using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Pagination;

public class PaginationExtensionsTests
{
    private class TestEntity : IReadOnlyEntity
    {
        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
            where TSelf : IReadOnlyEntity => (TSelf)(object)new TestEntity();

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item) =>
            item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

        public static EntityMetadata GetEntityMetadata() => new()
        {
            TableName = "test-table",
            Properties = Array.Empty<PropertyMetadata>()
        };
    }
    [Fact]
    public void Paginate_WithPageSizeAndNoToken_Success()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        PaginationExtensions.Paginate(builder, new PaginationRequest(10, ""));
        var request = builder.ToQueryRequest();

        request.Limit.Should().Be(10);
        request.ExclusiveStartKey.Should().BeEmpty();
    }

    [Fact]
    public void Paginate_WithPageSizeAndToken_Success()
    {
        var lastKey = new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1" } },
            { "sk", new AttributeValue { S = "test" } }
        };
        var queryResponse = new QueryResponse { LastEvaluatedKey = lastKey };
        var token = queryResponse.GetEncodedPaginationToken();

        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Paginate(new PaginationRequest(10, token));
        var request = builder.ToQueryRequest();

        request.Limit.Should().Be(10);
        request.ExclusiveStartKey.Should().NotBeNull();
    }
}
