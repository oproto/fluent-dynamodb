using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Pagination;

public class PaginationExtensionsTests
{
    private class TestEntity { }
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
