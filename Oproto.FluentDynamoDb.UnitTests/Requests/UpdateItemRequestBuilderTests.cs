using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Collection("OperationContext")]
public class UpdateItemRequestBuilderTests
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
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Keys

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Keys.Should().HaveCount(1);
        req.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Should().ContainKey("sk");
        req.Key.Keys.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("1");
        req.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Should().ContainKey("sk");
        req.Key.Keys.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("1");
        req.Key["sk"].S.Should().Be("abcd");
    }

    #endregion Keys

    #region Attributes

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "1");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }

    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", true);
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes

    [Fact]
    public void WhereSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("#pk = :pk");
    }

    [Fact]
    public void SetSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Set("SET #pk = :pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.UpdateExpression.Should().Be("SET #pk = :pk");
    }

    #region ConsumedCapacity

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnItemCollectionMetrics();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    #endregion ConsumedCapacity

    #region ReturnValues

    [Fact]
    public void ReturnValuesNoneSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }

    [Fact]
    public void ReturnAllNewValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllNewValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_NEW);
    }

    [Fact]
    public void ReturnAllOldValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ReturnUpdatedNewValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedNewValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }

    [Fact]
    public void ReturnUpdatedOldValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedOldValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_OLD);
    }

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new UpdateItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }

    #endregion ReturnValues

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_Success_CompletesWithoutError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "new-name");

        var mockResponse = new UpdateItemResponse
        {
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.UpdateAsync();

        // Assert
        await mockClient.Received(1).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithCancellationToken_PassesTokenToClient()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        var cancellationToken = new CancellationToken();
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name");

        var mockResponse = new UpdateItemResponse();

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.UpdateAsync(cancellationToken);

        // Assert
        await mockClient.Received(1).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), cancellationToken);
    }

    #endregion UpdateAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task UpdateAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name");

        var mockResponse = new UpdateItemResponse
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

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("UpdateItem");
        context.TableName.Should().Be("TestTable");
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(2.5);
        context.ItemCollectionMetrics.Should().NotBeNull();
        context.ResponseMetadata.Should().NotBeNull();
        context.ResponseMetadata!.RequestId.Should().Be("test-request-id");
    }

    [Fact]
    public async Task UpdateAsync_WithReturnAllOld_PopulatesPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .ReturnAllOldValues();

        var oldAttributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "old-name" }
        };

        var mockResponse = new UpdateItemResponse
        {
            Attributes = oldAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().NotBeNull();
        context.PreOperationValues.Should().BeSameAs(oldAttributes);
        context.PreOperationValues!["pk"].S.Should().Be("test-id");
        context.PreOperationValues["name"].S.Should().Be("old-name");
        context.PostOperationValues.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithReturnAllNew_PopulatesPostOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .ReturnAllNewValues();

        var newAttributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "new-name" }
        };

        var mockResponse = new UpdateItemResponse
        {
            Attributes = newAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PostOperationValues.Should().NotBeNull();
        context.PostOperationValues.Should().BeSameAs(newAttributes);
        context.PostOperationValues!["pk"].S.Should().Be("test-id");
        context.PostOperationValues["name"].S.Should().Be("new-name");
        context.PreOperationValues.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithReturnUpdatedOld_PopulatesPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .ReturnUpdatedOldValues();

        var updatedOldAttributes = new Dictionary<string, AttributeValue>
        {
            ["name"] = new AttributeValue { S = "old-name" }
        };

        var mockResponse = new UpdateItemResponse
        {
            Attributes = updatedOldAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().NotBeNull();
        context.PreOperationValues.Should().BeSameAs(updatedOldAttributes);
        context.PreOperationValues!["name"].S.Should().Be("old-name");
    }

    [Fact]
    public async Task UpdateAsync_WithReturnUpdatedNew_PopulatesPostOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name")
            .ReturnUpdatedNewValues();

        var updatedNewAttributes = new Dictionary<string, AttributeValue>
        {
            ["name"] = new AttributeValue { S = "new-name" }
        };

        var mockResponse = new UpdateItemResponse
        {
            Attributes = updatedNewAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PostOperationValues.Should().NotBeNull();
        context.PostOperationValues.Should().BeSameAs(updatedNewAttributes);
        context.PostOperationValues!["name"].S.Should().Be("new-name");
    }

    [Fact]
    public async Task UpdateAsync_WithoutReturnValues_PopulatesNullPreAndPostOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name");

        var mockResponse = new UpdateItemResponse
        {
            Attributes = null,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteUpdateAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().BeNull();
        context.PostOperationValues.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient);
        
        builder.ForTable("TestTable")
            .WithKey("pk", "test-id")
            .Set("SET #name = :name");

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UpdateItemResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.UpdateAsync());

        // Verify inner exception is preserved
        exception.InnerException.Should().BeSameAs(originalException);

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    #endregion Context Population Tests

    #region Helper Methods

    /// <summary>
    /// Helper method to execute UpdateAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static async Task<OperationContextData?> ExecuteUpdateAndGetContextAsync(UpdateItemRequestBuilder<TestEntity> builder)
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await builder.UpdateAsync<TestEntity>();
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods


    #region Encryption Tests

    [Fact]
    public async Task EncryptParametersAsync_WithEncryptedParameter_EncryptsValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with encrypted parameter
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Setup mock encryptor to return encrypted bytes
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes("encrypted-value");
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes));

        var mockResponse = new UpdateItemResponse();
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.ToDynamoDbResponseAsync();

        // Assert
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "SensitiveField",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        // Verify the request was sent with encrypted value (as binary)
        await mockClient.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(req =>
                req.ExpressionAttributeValues[":p0"].B != null &&
                req.ExpressionAttributeValues[":p0"].B.ToArray().SequenceEqual(encryptedBytes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncryptParametersAsync_WithoutEncryptor_ThrowsException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        // Create expression context with encrypted parameter but no encryptor
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.ToDynamoDbResponseAsync());

        exception.Message.Should().Contain("IFieldEncryptor");
        exception.Message.Should().Contain("SensitiveField");
    }

    [Fact]
    public async Task EncryptParametersAsync_WithMultipleEncryptedParameters_EncryptsAll()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with multiple encrypted parameters
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data-1" },
            RequiresEncryption = true,
            PropertyName = "Field1",
            AttributeName = "field1"
        });

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p1",
            Value = new AttributeValue { S = "sensitive-data-2" },
            RequiresEncryption = true,
            PropertyName = "Field2",
            AttributeName = "field2"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field1 = :p0, #field2 = :p1")
            .WithAttribute("#field1", "field1")
            .WithAttribute("#field2", "field2")
            .WithValue(":p0", "sensitive-data-1")
            .WithValue(":p1", "sensitive-data-2");

        // Setup mock encryptor
        var encryptedBytes1 = System.Text.Encoding.UTF8.GetBytes("encrypted-1");
        var encryptedBytes2 = System.Text.Encoding.UTF8.GetBytes("encrypted-2");
        
        mockEncryptor.EncryptAsync(
            Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "sensitive-data-1"),
            "Field1",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes1));

        mockEncryptor.EncryptAsync(
            Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "sensitive-data-2"),
            "Field2",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(encryptedBytes2));

        var mockResponse = new UpdateItemResponse();
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.ToDynamoDbResponseAsync();

        // Assert
        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "Field1",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());

        await mockEncryptor.Received(1).EncryptAsync(
            Arg.Any<byte[]>(),
            "Field2",
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncryptParametersAsync_WithEncryptionFailure_ThrowsFieldEncryptionException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with encrypted parameter
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "sensitive-data" },
            RequiresEncryption = true,
            PropertyName = "SensitiveField",
            AttributeName = "sensitive_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "sensitive_field")
            .WithValue(":p0", "sensitive-data");

        // Setup mock encryptor to throw exception
        mockEncryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]>(new Exception("KMS error")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FieldEncryptionException>(
            () => builder.ToDynamoDbResponseAsync());

        exception.Message.Should().Contain("SensitiveField");
        exception.InnerException?.Message.Should().Contain("KMS error");
    }

    [Fact]
    public async Task EncryptParametersAsync_WithNoEncryptedParameters_DoesNotCallEncryptor()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockEncryptor = Substitute.For<IFieldEncryptor>();
        
        var builder = new UpdateItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .SetFieldEncryptor(mockEncryptor);

        // Create expression context with non-encrypted parameter
        var context = new Oproto.FluentDynamoDb.Expressions.ExpressionContext(
            builder.GetAttributeValueHelper(),
            builder.GetAttributeNameHelper(),
            null,
            Oproto.FluentDynamoDb.Expressions.ExpressionValidationMode.None);

        context.ParameterMetadata.Add(new Oproto.FluentDynamoDb.Expressions.ParameterMetadata
        {
            ParameterName = ":p0",
            Value = new AttributeValue { S = "normal-data" },
            RequiresEncryption = false,
            PropertyName = "NormalField",
            AttributeName = "normal_field"
        });

        builder.SetExpressionContext(context);
        builder.Set("SET #field = :p0")
            .WithAttribute("#field", "normal_field")
            .WithValue(":p0", "normal-data");

        var mockResponse = new UpdateItemResponse();
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.ToDynamoDbResponseAsync();

        // Assert
        await mockEncryptor.DidNotReceive().EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>());
    }

    #endregion Encryption Tests

}
