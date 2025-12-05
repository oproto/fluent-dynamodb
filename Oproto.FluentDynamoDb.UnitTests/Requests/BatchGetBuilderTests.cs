using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class BatchGetBuilderTests
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

    private class SecondTestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as SecondTestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["value"] = new AttributeValue { S = testEntity?.Value ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var entity = new SecondTestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Value = item.TryGetValue("value", out var value) ? value.S : string.Empty
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
                TableName = "second-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }
    }


    #region 20.1 Test Add() method and request extraction

    [Fact]
    public void Add_GetBuilder_AddsOperationToBatch()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        batchBuilder.Add(getBuilder);

        // Assert - verify operation was added by checking ExecuteAsync validation
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        }).Result;
        
        // Should fail with AWS SDK error, not "no operations" error
        exception.Message.Should().NotContain("no operations");
    }

    [Fact]
    public async Task Add_GetBuilder_GroupsByTableName()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" } }
                    },
                    ["SecondTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id3" } }
                    }
                }
            });

        var getBuilder1 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var getBuilder2 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id2");

        var getBuilder3 = new GetItemRequestBuilder<SecondTestEntity>(mockClient)
            .ForTable("SecondTable")
            .WithKey("pk", "id3");

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder
            .Add(getBuilder1)
            .Add(getBuilder2)
            .Add(getBuilder3)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchGetItemAsync(
            Arg.Is<BatchGetItemRequest>(req =>
                req.RequestItems.Count == 2 &&
                req.RequestItems["TestTable"].Keys.Count == 2 &&
                req.RequestItems["SecondTable"].Keys.Count == 1),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Add_GetBuilder_PreservesProjectionExpression()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .WithProjection("name, email");

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder.Add(getBuilder).ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchGetItemAsync(
            Arg.Is<BatchGetItemRequest>(req =>
                req.RequestItems["TestTable"].ProjectionExpression == "name, email"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_GetBuilder_PreservesConsistentRead()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id")
            .UsingConsistentRead();

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder.Add(getBuilder).ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchGetItemAsync(
            Arg.Is<BatchGetItemRequest>(req =>
                req.RequestItems["TestTable"].ConsistentRead == true),
            Arg.Any<CancellationToken>());
    }


    #endregion

    #region 20.2 Test client inference and configuration

    [Fact]
    public void ClientInference_ExtractsFromFirstBuilder()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        batchBuilder.Add(getBuilder);

        // Assert - client should be inferred, so ExecuteAsync should not throw "no client" error
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        }).Result;
        
        // Should fail with AWS SDK error or similar, not "no client" error
        exception.Message.Should().NotContain("No DynamoDB client specified");
    }

    [Fact]
    public void ClientInference_DetectsMismatch_ThrowsException()
    {
        // Arrange
        var mockClient1 = Substitute.For<IAmazonDynamoDB>();
        var mockClient2 = Substitute.For<IAmazonDynamoDB>();
        
        var getBuilder1 = new GetItemRequestBuilder<TestEntity>(mockClient1)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var getBuilder2 = new GetItemRequestBuilder<TestEntity>(mockClient2)
            .ForTable("TestTable")
            .WithKey("pk", "id2");

        var batchBuilder = new BatchGetBuilder();

        // Act & Assert
        batchBuilder.Add(getBuilder1);
        
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            batchBuilder.Add(getBuilder2);
        });
        
        exception.Message.Should().Contain("same DynamoDB client instance");
    }

    [Fact]
    public async Task WithClient_OverridesInference()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        
        explicitClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder
            .Add(getBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync();

        // Assert - explicit client should be used
        await explicitClient.Received(1).BatchGetItemAsync(
            Arg.Any<BatchGetItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().BatchGetItemAsync(
            Arg.Any<BatchGetItemRequest>(),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ExecuteAsync_ClientParameter_HasHighestPrecedence()
    {
        // Arrange
        var inferredClient = Substitute.For<IAmazonDynamoDB>();
        var explicitClient = Substitute.For<IAmazonDynamoDB>();
        var parameterClient = Substitute.For<IAmazonDynamoDB>();
        
        parameterClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(inferredClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder
            .Add(getBuilder)
            .WithClient(explicitClient)
            .ExecuteAsync(parameterClient);

        // Assert - parameter client should be used
        await parameterClient.Received(1).BatchGetItemAsync(
            Arg.Any<BatchGetItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await explicitClient.DidNotReceive().BatchGetItemAsync(
            Arg.Any<BatchGetItemRequest>(),
            Arg.Any<CancellationToken>());
        
        await inferredClient.DidNotReceive().BatchGetItemAsync(
            Arg.Any<BatchGetItemRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnConsumedCapacity_SetsCorrectValue()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        await batchBuilder
            .Add(getBuilder)
            .ReturnConsumedCapacity(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // Assert
        await mockClient.Received(1).BatchGetItemAsync(
            Arg.Is<BatchGetItemRequest>(req =>
                req.ReturnConsumedCapacity == Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL),
            Arg.Any<CancellationToken>());
    }

    #endregion


    #region 20.3 Test validation

    [Fact]
    public async Task ExecuteAsync_EmptyBatch_ThrowsException()
    {
        // Arrange
        var batchBuilder = new BatchGetBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("no operations");
    }

    [Fact]
    public async Task ExecuteAsync_MoreThan100Operations_ThrowsException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var batchBuilder = new BatchGetBuilder();

        // Add 101 operations
        for (int i = 0; i < 101; i++)
        {
            var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithKey("pk", $"id-{i}");
            
            batchBuilder.Add(getBuilder);
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.ExecuteAsync();
        });
        
        exception.Message.Should().Contain("101 operations");
        exception.Message.Should().Contain("maximum of 100");
        exception.Message.Should().Contain("chunking");
    }

    [Fact]
    public async Task ExecuteAsync_MissingClient_ThrowsClearException()
    {
        // Arrange
        // Create a builder without a client (using null)
        var getBuilder = new GetItemRequestBuilder<TestEntity>(null!)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await batchBuilder.Add(getBuilder).ExecuteAsync();
        });
        
        exception.Message.Should().Contain("No DynamoDB client specified");
        exception.Message.Should().Contain("ExecuteAsync()");
        exception.Message.Should().Contain("WithClient()");
    }

    #endregion


    #region 20.4 Test response deserialization

    [Fact]
    public async Task GetItem_DeserializesCorrectTypeAtIndex()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "test-id" },
                            ["name"] = new AttributeValue { S = "Test Name" }
                        }
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var response = await batchBuilder.Add(getBuilder).ExecuteAsync();
        var entity = response.GetItem<TestEntity>(0);

        // Assert
        entity.Should().NotBeNull();
        entity!.Id.Should().Be("test-id");
        entity.Name.Should().Be("Test Name");
    }

    [Fact]
    public async Task GetItems_DeserializesMultipleIndices()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "Name1" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "Name2" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id3" }, ["name"] = new AttributeValue { S = "Name3" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id4" }, ["name"] = new AttributeValue { S = "Name4" } }
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        for (int i = 1; i <= 4; i++)
        {
            var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithKey("pk", $"id{i}");
            batchBuilder.Add(getBuilder);
        }

        // Act
        var response = await batchBuilder.ExecuteAsync();
        var entities = response.GetItems<TestEntity>(0, 2, 3);

        // Assert
        entities.Should().HaveCount(3);
        entities[0]!.Id.Should().Be("id1");
        entities[1]!.Id.Should().Be("id3");
        entities[2]!.Id.Should().Be("id4");
    }


    [Fact]
    public async Task GetItemsRange_DeserializesContiguousRange()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "Name1" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "Name2" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id3" }, ["name"] = new AttributeValue { S = "Name3" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id4" }, ["name"] = new AttributeValue { S = "Name4" } }
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        for (int i = 1; i <= 4; i++)
        {
            var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
                .ForTable("TestTable")
                .WithKey("pk", $"id{i}");
            batchBuilder.Add(getBuilder);
        }

        // Act
        var response = await batchBuilder.ExecuteAsync();
        var entities = response.GetItemsRange<TestEntity>(1, 3);

        // Assert
        entities.Should().HaveCount(3);
        entities[0]!.Id.Should().Be("id2");
        entities[1]!.Id.Should().Be("id3");
        entities[2]!.Id.Should().Be("id4");
    }

    [Fact]
    public async Task GetItem_NullItem_ReturnsNull()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        null!,
                        new Dictionary<string, AttributeValue>()
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        var getBuilder1 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "missing-id");
        var getBuilder2 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "empty-id");
        
        batchBuilder.Add(getBuilder1).Add(getBuilder2);

        // Act
        var response = await batchBuilder.ExecuteAsync();
        var entity1 = response.GetItem<TestEntity>(0);
        var entity2 = response.GetItem<TestEntity>(1);

        // Assert
        entity1.Should().BeNull();
        entity2.Should().BeNull();
    }


    [Fact]
    public async Task GetItem_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } }
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var response = await batchBuilder.Add(getBuilder).ExecuteAsync();

        // Assert
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => response.GetItem<TestEntity>(-1));
        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => response.GetItem<TestEntity>(1));
        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => response.GetItem<TestEntity>(100));
    }

    [Fact]
    public async Task UnprocessedKeys_ExposesUnprocessedItems()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var unprocessedKeys = new Dictionary<string, KeysAndAttributes>
        {
            ["TestTable"] = new KeysAndAttributes
            {
                Keys = new List<Dictionary<string, AttributeValue>>
                {
                    new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "unprocessed-id" } }
                }
            }
        };

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } }
                    }
                },
                UnprocessedKeys = unprocessedKeys
            });

        var getBuilder1 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");
        
        var getBuilder2 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "unprocessed-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var response = await batchBuilder.Add(getBuilder1).Add(getBuilder2).ExecuteAsync();

        // Assert
        response.UnprocessedKeys.Should().NotBeNull();
        response.UnprocessedKeys.Should().ContainKey("TestTable");
        response.UnprocessedKeys["TestTable"].Keys.Should().HaveCount(1);
    }


    [Fact]
    public async Task HasUnprocessedKeys_ReturnsTrueWhenUnprocessedKeysExist()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var unprocessedKeys = new Dictionary<string, KeysAndAttributes>
        {
            ["TestTable"] = new KeysAndAttributes
            {
                Keys = new List<Dictionary<string, AttributeValue>>
                {
                    new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "unprocessed-id" } }
                }
            }
        };

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } }
                    }
                },
                UnprocessedKeys = unprocessedKeys
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var response = await batchBuilder.Add(getBuilder).ExecuteAsync();

        // Assert
        response.HasUnprocessedKeys.Should().BeTrue();
    }

    [Fact]
    public async Task HasUnprocessedKeys_ReturnsFalseWhenNoUnprocessedKeys()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" } }
                    }
                },
                UnprocessedKeys = new Dictionary<string, KeysAndAttributes>()
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var response = await batchBuilder.Add(getBuilder).ExecuteAsync();

        // Assert
        response.HasUnprocessedKeys.Should().BeFalse();
    }


    [Fact]
    public async Task ExecuteAndMapAsync_SingleType_ReturnsEntity()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "test-id" },
                            ["name"] = new AttributeValue { S = "Test Name" }
                        }
                    }
                }
            });

        var getBuilder = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-id");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var entity = await batchBuilder.Add(getBuilder).ExecuteAndMapAsync<TestEntity>();

        // Assert
        entity.Should().NotBeNull();
        entity!.Id.Should().Be("test-id");
        entity.Name.Should().Be("Test Name");
    }

    [Fact]
    public async Task ExecuteAndMapAsync_TwoTypes_ReturnsTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "id1" },
                            ["name"] = new AttributeValue { S = "Name1" }
                        }
                    },
                    ["SecondTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "id2" },
                            ["value"] = new AttributeValue { S = "Value2" }
                        }
                    }
                }
            });

        var getBuilder1 = new GetItemRequestBuilder<TestEntity>(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1");
        
        var getBuilder2 = new GetItemRequestBuilder<SecondTestEntity>(mockClient)
            .ForTable("SecondTable")
            .WithKey("pk", "id2");

        var batchBuilder = new BatchGetBuilder();

        // Act
        var (entity1, entity2) = await batchBuilder
            .Add(getBuilder1)
            .Add(getBuilder2)
            .ExecuteAndMapAsync<TestEntity, SecondTestEntity>();

        // Assert
        entity1.Should().NotBeNull();
        entity1!.Id.Should().Be("id1");
        entity1.Name.Should().Be("Name1");
        
        entity2.Should().NotBeNull();
        entity2!.Id.Should().Be("id2");
        entity2.Value.Should().Be("Value2");
    }


    [Fact]
    public async Task ExecuteAndMapAsync_ThreeTypes_ReturnsTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "Name1" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id3" }, ["name"] = new AttributeValue { S = "Name3" } }
                    },
                    ["SecondTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" }, ["value"] = new AttributeValue { S = "Value2" } }
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        batchBuilder
            .Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", "id1"))
            .Add(new GetItemRequestBuilder<SecondTestEntity>(mockClient).ForTable("SecondTable").WithKey("pk", "id2"))
            .Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", "id3"));

        // Act
        var (entity1, entity2, entity3) = await batchBuilder
            .ExecuteAndMapAsync<TestEntity, SecondTestEntity, TestEntity>();

        // Assert
        entity1.Should().NotBeNull();
        entity1!.Id.Should().Be("id1");
        
        entity2.Should().NotBeNull();
        entity2!.Id.Should().Be("id2");
        
        entity3.Should().NotBeNull();
        entity3!.Id.Should().Be("id3");
    }

    [Fact]
    public async Task ExecuteAndMapAsync_FourTypes_ReturnsTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "Name1" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "Name2" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id3" }, ["name"] = new AttributeValue { S = "Name3" } },
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id4" }, ["name"] = new AttributeValue { S = "Name4" } }
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        for (int i = 1; i <= 4; i++)
        {
            batchBuilder.Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", $"id{i}"));
        }

        // Act
        var (e1, e2, e3, e4) = await batchBuilder
            .ExecuteAndMapAsync<TestEntity, TestEntity, TestEntity, TestEntity>();

        // Assert
        e1.Should().NotBeNull();
        e1!.Id.Should().Be("id1");
        e2.Should().NotBeNull();
        e2!.Id.Should().Be("id2");
        e3.Should().NotBeNull();
        e3!.Id.Should().Be("id3");
        e4.Should().NotBeNull();
        e4!.Id.Should().Be("id4");
    }


    [Fact]
    public async Task ExecuteAndMapAsync_EightTypes_ReturnsTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var responses = new List<Dictionary<string, AttributeValue>>();
        for (int i = 1; i <= 8; i++)
        {
            responses.Add(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"id{i}" },
                ["name"] = new AttributeValue { S = $"Name{i}" }
            });
        }

        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = responses
                }
            });

        var batchBuilder = new BatchGetBuilder();
        for (int i = 1; i <= 8; i++)
        {
            batchBuilder.Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", $"id{i}"));
        }

        // Act
        var (e1, e2, e3, e4, e5, e6, e7, e8) = await batchBuilder
            .ExecuteAndMapAsync<TestEntity, TestEntity, TestEntity, TestEntity, TestEntity, TestEntity, TestEntity, TestEntity>();

        // Assert
        e1.Should().NotBeNull();
        e1!.Id.Should().Be("id1");
        e8.Should().NotBeNull();
        e8!.Id.Should().Be("id8");
    }

    [Fact]
    public async Task ExecuteAndMapAsync_WithNullItems_ReturnsNullsInTuple()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>
                {
                    ["TestTable"] = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue> { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "Name1" } },
                        null!
                    }
                }
            });

        var batchBuilder = new BatchGetBuilder();
        batchBuilder
            .Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", "id1"))
            .Add(new GetItemRequestBuilder<TestEntity>(mockClient).ForTable("TestTable").WithKey("pk", "missing"));

        // Act
        var (entity1, entity2) = await batchBuilder.ExecuteAndMapAsync<TestEntity, TestEntity>();

        // Assert
        entity1.Should().NotBeNull();
        entity1!.Id.Should().Be("id1");
        entity2.Should().BeNull();
    }

    #endregion
}
