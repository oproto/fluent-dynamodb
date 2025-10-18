using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class DeleteItemRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Key Specification Tests

    [Fact]
    public void WithKeySingleKeyAttributeValueSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var keyValue = new AttributeValue { S = "test-key" };
        builder.WithKey("pk", keyValue);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(1);
        req.Key["pk"].Should().Be(keyValue);
    }

    [Fact]
    public void WithKeyCompositeKeyAttributeValueSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var pkValue = new AttributeValue { S = "test-pk" };
        var skValue = new AttributeValue { S = "test-sk" };
        builder.WithKey("pk", pkValue, "sk", skValue);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(2);
        req.Key["pk"].Should().Be(pkValue);
        req.Key["sk"].Should().Be(skValue);
    }

    [Fact]
    public void WithKeyCompositeKeyAttributeValueWithNullSortKeySuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var pkValue = new AttributeValue { S = "test-pk" };
        builder.WithKey("pk", pkValue, null, null);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(1);
        req.Key["pk"].Should().Be(pkValue);
    }

    [Fact]
    public void WithKeySingleKeyStringSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "test-key");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(1);
        req.Key["pk"].S.Should().Be("test-key");
    }

    [Fact]
    public void WithKeyCompositeKeyStringSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "test-pk", "sk", "test-sk");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("test-pk");
        req.Key["sk"].S.Should().Be("test-sk");
    }

    [Fact]
    public void WithKeyMultipleCallsSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "test-pk");
        builder.WithKey("sk", "test-sk");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("test-pk");
        req.Key["sk"].S.Should().Be("test-sk");
    }

    #endregion Key Specification Tests

    #region Condition Expression Tests

    [Fact]
    public void WhereSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Where("attribute_exists(#attr)");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("attribute_exists(#attr)");
    }

    [Fact]
    public void WhereWithComplexConditionSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#status = :status AND #version = :version");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("#status = :status AND #version = :version");
    }

    #endregion Condition Expression Tests

    #region Attribute Names Tests

    [Fact]
    public void WithAttributesSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string> { { "#pk", "pk" }, { "#status", "status" } });
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributesUsingLambdaSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(attributes => 
        {
            attributes.Add("#pk", "pk");
            attributes.Add("#status", "status");
        });
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributeSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToDeleteItemRequest();
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
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue> 
        { 
            { ":pk", new AttributeValue { S = "1" } },
            { ":status", new AttributeValue { S = "active" } }
        });
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValuesLambdaSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(attributes => 
        {
            attributes.Add(":pk", new AttributeValue { S = "1" });
            attributes.Add(":status", new AttributeValue { S = "active" });
        });
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValueStringSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "test-value");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("test-value");
    }

    [Fact]
    public void WithValueStringNullSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", (string?)null);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // null string values are not added when conditionalUse is true
    }

    [Fact]
    public void WithValueBooleanSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", true);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void WithValueBooleanNullSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", (bool?)null);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeNull(); // null bool becomes null in SDK v4
        req.ExpressionAttributeValues[":active"].IsBOOLSet.Should().BeFalse(); // IsBOOLSet indicates it was null
    }

    [Fact]
    public void WithValueDecimalSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", 99.99m);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be("99.99");
    }

    [Fact]
    public void WithValueDecimalNullSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", (decimal?)null);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be(""); // null decimal becomes empty string
    }

    [Fact]
    public void WithValueStringDictionarySuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        builder.WithValue(":map", mapValue);
        var req = builder.ToDeleteItemRequest();
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
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, AttributeValue> 
        { 
            { "key1", new AttributeValue { S = "value1" } }, 
            { "key2", new AttributeValue { N = "42" } } 
        };
        builder.WithValue(":map", mapValue);
        var req = builder.ToDeleteItemRequest();
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
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "test-value", conditionalUse: false);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // conditionalUse: false means nothing is added
    }

    [Fact]
    public void WithValueConditionalUseTrueSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "test-value", conditionalUse: true);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("test-value");
    }

    #endregion Attribute Values Tests

    #region Return Values Tests

    [Fact]
    public void ReturnAllOldValuesSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ReturnNoneSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }

    [Fact]
    public void DefaultReturnValuesSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().BeNull();
    }

    #endregion Return Values Tests

    #region Consumed Capacity Tests

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void ReturnConsumedCapacityNoneSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.NONE);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.NONE);
    }

    [Fact]
    public void DefaultConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().BeNull();
    }

    #endregion Consumed Capacity Tests

    #region Item Collection Metrics Tests

    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnItemCollectionMetrics();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    [Fact]
    public void DefaultItemCollectionMetricsSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().BeNull();
    }

    #endregion Item Collection Metrics Tests

    #region Condition Check Failure Tests

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void DefaultReturnValuesOnConditionCheckFailureSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }

    #endregion Condition Check Failure Tests

    #region Request Building and Execution Tests

    [Fact]
    public void ToDeleteItemRequestSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable")
               .WithKey("pk", "test-key")
               .Where("#status = :status")
               .WithAttribute("#status", "status")
               .WithValue(":status", "active")
               .ReturnAllOldValues()
               .ReturnTotalConsumedCapacity()
               .ReturnItemCollectionMetrics()
               .ReturnOldValuesOnConditionCheckFailure();

        var req = builder.ToDeleteItemRequest();
        
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
        req.Key.Should().HaveCount(1);
        req.Key["pk"].S.Should().Be("test-key");
        req.ConditionExpression.Should().Be("#status = :status");
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public async Task ExecuteAsyncSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new DeleteItemResponse();
        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new DeleteItemRequestBuilder(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-key");

        var response = await builder.ExecuteAsync();

        response.Should().Be(expectedResponse);
        await mockClient.Received(1).DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncWithCancellationTokenSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new DeleteItemResponse();
        var cancellationToken = new CancellationToken();
        
        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), cancellationToken)
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new DeleteItemRequestBuilder(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-key");

        var response = await builder.ExecuteAsync(cancellationToken);

        response.Should().Be(expectedResponse);
        await mockClient.Received(1).DeleteItemAsync(Arg.Any<DeleteItemRequest>(), cancellationToken);
    }

    #endregion Request Building and Execution Tests

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterfaceChainSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        
        var result = builder
            .ForTable("TestTable")
            .WithKey("pk", "test-key")
            .Where("#status = :status")
            .WithAttribute("#status", "status")
            .WithValue(":status", "active")
            .ReturnAllOldValues()
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics()
            .ReturnOldValuesOnConditionCheckFailure();

        result.Should().Be(builder);
        
        var req = result.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void MultipleAttributeAndValueCallsSuccess()
    {
        var builder = new DeleteItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        
        builder.WithAttribute("#pk", "pk")
               .WithAttribute("#status", "status")
               .WithValue(":pk", "test-key")
               .WithValue(":status", "active")
               .WithValue(":version", 1m);

        var req = builder.ToDeleteItemRequest();
        
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(3);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("test-key");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ExpressionAttributeValues[":version"].N.Should().Be("1");
    }

    #endregion Fluent Interface Tests
}