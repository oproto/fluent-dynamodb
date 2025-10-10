using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class ScanRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Filter Expression Tests

    [Fact]
    public void WithFilterSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#status = :status");
    }

    [Fact]
    public void WithFilterComplexExpressionSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#status = :status AND #price > :minPrice");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#status = :status AND #price > :minPrice");
    }

    #endregion Filter Expression Tests

    #region Projection Expression Tests

    [Fact]
    public void WithProjectionSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk, #sk, #status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("#pk, #sk, #status");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
    }

    [Fact]
    public void WithProjectionSingleAttributeSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("#pk");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
    }

    #endregion Projection Expression Tests

    #region Index Tests

    [Fact]
    public void UsingIndexSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void UsingIndexWithFilterSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1").WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
        req.FilterExpression.Should().Be("#status = :status");
    }

    #endregion Index Tests

    #region Limit and Pagination Tests

    [Fact]
    public void TakeSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Take(10);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Limit.Should().Be(10);
    }

    [Fact]
    public void StartAtSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var startKey = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "test-pk" } },
            { "sk", new AttributeValue { S = "test-sk" } }
        };
        builder.StartAt(startKey);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExclusiveStartKey.Should().BeEquivalentTo(startKey);
    }

    #endregion Limit and Pagination Tests

    #region Parallel Scan Tests

    [Fact]
    public void WithSegmentSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(0, 4);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(0);
        req.TotalSegments.Should().Be(4);
    }

    [Fact]
    public void WithSegmentMultipleSegmentsSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(2, 8);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(2);
        req.TotalSegments.Should().Be(8);
    }

    [Fact]
    public void WithSegmentAndFilterSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(1, 4).WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(1);
        req.TotalSegments.Should().Be(4);
        req.FilterExpression.Should().Be("#status = :status");
    }

    #endregion Parallel Scan Tests

    #region Consistency Tests

    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void DefaultConsistentReadSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeFalse();
    }

    #endregion Consistency Tests

    #region Count Tests

    [Fact]
    public void CountSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Count();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Select.Should().Be(Select.COUNT);
    }

    [Fact]
    public void CountOverridesProjectionSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk, #sk").Count();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Select.Should().Be(Select.COUNT);
        req.ProjectionExpression.Should().Be("#pk, #sk"); // Projection expression remains but Select is COUNT
    }

    #endregion Count Tests

    #region Consumed Capacity Tests

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnIndexConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnIndexConsumedCapacity();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.NONE);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.NONE);
    }

    [Fact]
    public void DefaultConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().BeNull();
    }

    #endregion Consumed Capacity Tests
 
   #region Attribute Names Tests

    [Fact]
    public void WithAttributesSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string> { { "#pk", "pk" }, { "#status", "status" } });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributesUsingLambdaSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(attributes => 
        {
            attributes.Add("#pk", "pk");
            attributes.Add("#status", "status");
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributeSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    #endregion Attribute Names Tests

    #region Attribute Values Tests

    [Fact]
    public void WithValuesSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue> 
        { 
            { ":pk", new AttributeValue { S = "1" } },
            { ":status", new AttributeValue { S = "active" } }
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValuesLambdaSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(attributes => 
        {
            attributes.Add(":pk", new AttributeValue { S = "1" });
            attributes.Add(":status", new AttributeValue { S = "active" });
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValueStringSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValueStringNullSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", (string?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // null string values are not added when conditionalUse is true
    }

    [Fact]
    public void WithValueBooleanSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", true);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void WithValueBooleanNullSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", (bool?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeFalse(); // null bool becomes false
        req.ExpressionAttributeValues[":active"].IsBOOLSet.Should().BeFalse(); // IsBOOLSet indicates it was null
    }

    [Fact]
    public void WithValueDecimalSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", 99.99m);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be("99.99");
    }

    [Fact]
    public void WithValueDecimalNullSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", (decimal?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be(""); // null decimal becomes empty string
    }

    [Fact]
    public void WithValueStringDictionarySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        builder.WithValue(":map", mapValue);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":map"].M.Should().HaveCount(2);
        req.ExpressionAttributeValues[":map"].M["key1"].S.Should().Be("value1");
        req.ExpressionAttributeValues[":map"].M["key2"].S.Should().Be("value2");
    }

    [Fact]
    public void WithValueAttributeValueDictionarySuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, AttributeValue> 
        { 
            { "key1", new AttributeValue { S = "value1" } }, 
            { "key2", new AttributeValue { N = "42" } } 
        };
        builder.WithValue(":map", mapValue);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":map"].M.Should().HaveCount(2);
        req.ExpressionAttributeValues[":map"].M["key1"].S.Should().Be("value1");
        req.ExpressionAttributeValues[":map"].M["key2"].N.Should().Be("42");
    }

    [Fact]
    public void WithValueConditionalUseFalseSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active", conditionalUse: false);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // conditionalUse: false means nothing is added
    }

    [Fact]
    public void WithValueConditionalUseTrueSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active", conditionalUse: true);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    #endregion Attribute Values Tests

    #region Request Building and Execution Tests

    [Fact]
    public void ToScanRequestSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable")
               .WithFilter("#status = :status")
               .WithProjection("#pk, #sk, #status")
               .UsingIndex("gsi1")
               .Take(10)
               .UsingConsistentRead()
               .WithSegment(0, 4)
               .WithAttribute("#status", "status")
               .WithValue(":status", "active")
               .ReturnTotalConsumedCapacity();

        var req = builder.ToScanRequest();
        
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
        req.FilterExpression.Should().Be("#status = :status");
        req.ProjectionExpression.Should().Be("#pk, #sk, #status");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
        req.IndexName.Should().Be("gsi1");
        req.Limit.Should().Be(10);
        req.ConsistentRead.Should().BeTrue();
        req.Segment.Should().Be(0);
        req.TotalSegments.Should().Be(4);
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public async Task ExecuteAsyncSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new ScanResponse();
        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new ScanRequestBuilder(mockClient);
        builder.ForTable("TestTable");

        var response = await builder.ExecuteAsync();

        response.Should().Be(expectedResponse);
        await mockClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncWithCancellationTokenSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new ScanResponse();
        var cancellationToken = new CancellationToken();
        
        mockClient.ScanAsync(Arg.Any<ScanRequest>(), cancellationToken)
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new ScanRequestBuilder(mockClient);
        builder.ForTable("TestTable");

        var response = await builder.ExecuteAsync(cancellationToken);

        response.Should().Be(expectedResponse);
        await mockClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), cancellationToken);
    }

    #endregion Request Building and Execution Tests

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterfaceChainSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        
        var result = builder
            .ForTable("TestTable")
            .WithFilter("#status = :status")
            .WithProjection("#pk, #sk")
            .UsingIndex("gsi1")
            .Take(10)
            .UsingConsistentRead()
            .WithSegment(0, 4)
            .WithAttribute("#status", "status")
            .WithValue(":status", "active")
            .ReturnTotalConsumedCapacity();

        result.Should().Be(builder);
        
        var req = result.ToScanRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void MultipleAttributeAndValueCallsSuccess()
    {
        var builder = new ScanRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        
        builder.WithAttribute("#pk", "pk")
               .WithAttribute("#status", "status")
               .WithValue(":pk", "test-key")
               .WithValue(":status", "active")
               .WithValue(":price", 99.99m);

        var req = builder.ToScanRequest();
        
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(3);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("test-key");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ExpressionAttributeValues[":price"].N.Should().Be("99.99");
    }

    #endregion Fluent Interface Tests
}