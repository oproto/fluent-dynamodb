using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Collection("OperationContext")]
public class DeleteItemRequestBuilderTests
{
    private class TestEntity : IDynamoDbEntity
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
    public void ForTableSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Key Specification Tests

    [Fact]
    public void WithKeySingleKeyAttributeValueSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Where("attribute_exists(#attr)");
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("attribute_exists(#attr)");
    }

    [Fact]
    public void WhereWithComplexConditionSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", (string?)null);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // null string values are not added when conditionalUse is true
    }

    [Fact]
    public void WithValueBooleanSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "test-value", conditionalUse: false);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // conditionalUse: false means nothing is added
    }

    [Fact]
    public void WithValueConditionalUseTrueSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ReturnNoneSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }

    [Fact]
    public void DefaultReturnValuesSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().BeNull();
    }

    #endregion Return Values Tests

    #region Consumed Capacity Tests

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void ReturnConsumedCapacityNoneSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.NONE);
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.NONE);
    }

    [Fact]
    public void DefaultConsumedCapacitySuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().BeNull();
    }

    #endregion Consumed Capacity Tests

    #region Item Collection Metrics Tests

    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnItemCollectionMetrics();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    [Fact]
    public void DefaultItemCollectionMetricsSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().BeNull();
    }

    #endregion Item Collection Metrics Tests

    #region Condition Check Failure Tests

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void DefaultReturnValuesOnConditionCheckFailureSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToDeleteItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }

    #endregion Condition Check Failure Tests

    #region Request Building and Execution Tests

    [Fact]
    public void ToDeleteItemRequestSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
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

        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-key");

        await builder.DeleteAsync<TestEntity>();

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

        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-key");

        await builder.DeleteAsync<TestEntity>(cancellationToken);

        await mockClient.Received(1).DeleteItemAsync(Arg.Any<DeleteItemRequest>(), cancellationToken);
    }

    #endregion Request Building and Execution Tests

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterfaceChainSuccess()
    {
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

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
        var builder = new DeleteItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

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

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_Success_CompletesWithoutError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new DeleteItemResponse
        {
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.DeleteAsync();

        // Assert
        await mockClient.Received(1).DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithCancellationToken_PassesTokenToClient()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        var cancellationToken = new CancellationToken();
        
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new DeleteItemResponse();

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.DeleteAsync(cancellationToken);

        // Assert
        await mockClient.Received(1).DeleteItemAsync(Arg.Any<DeleteItemRequest>(), cancellationToken);
    }

    #endregion DeleteAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task DeleteAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new DeleteItemResponse
        {
            ConsumedCapacity = new ConsumedCapacity
            {
                TableName = "TestTable",
                CapacityUnits = 2.5
            },
            ItemCollectionMetrics = new ItemCollectionMetrics
            {
                ItemCollectionKey = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "test-id" }
                }
            },
            ResponseMetadata = new ResponseMetadata
            {
                RequestId = "test-request-id"
            }
        };

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var context = await ExecuteDeleteAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("DeleteItem");
        context.TableName.Should().Be("TestTable");
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(2.5);
        context.ItemCollectionMetrics.Should().NotBeNull();
        context.ResponseMetadata.Should().NotBeNull();
        context.ResponseMetadata!.RequestId.Should().Be("test-request-id");
    }

    [Fact]
    public async Task DeleteAsync_WithReturnAllOld_PopulatesPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .ReturnAllOldValues();

        var deletedAttributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "deleted-name" }
        };

        var mockResponse = new DeleteItemResponse
        {
            Attributes = deletedAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var context = await ExecuteDeleteAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().NotBeNull();
        context.PreOperationValues.Should().BeSameAs(deletedAttributes);
        context.PreOperationValues!["pk"].S.Should().Be("test-id");
        context.PreOperationValues["name"].S.Should().Be("deleted-name");
    }

    [Fact]
    public async Task DeleteAsync_WithoutReturnValues_PopulatesNullPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new DeleteItemResponse
        {
            Attributes = null,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var context = await ExecuteDeleteAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new DeleteItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeleteItemResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.DeleteAsync());

        // Verify inner exception is preserved
        exception.InnerException.Should().BeSameAs(originalException);

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    #endregion Context Population Tests

    #region Helper Methods

    private static async Task<OperationContextData?> ExecuteDeleteAndGetContextAsync(DeleteItemRequestBuilder<TestEntity> builder)
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await builder.DeleteAsync();
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods
}
