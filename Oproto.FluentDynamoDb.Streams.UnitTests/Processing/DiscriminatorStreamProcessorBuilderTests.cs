using AwesomeAssertions;
using Oproto.FluentDynamoDb.Streams.Extensions;
using Oproto.FluentDynamoDb.Streams.Processing;
using LambdaAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace Oproto.FluentDynamoDb.Streams.UnitTests.Processing;

public class DiscriminatorStreamProcessorBuilderTests
{
    // Test entities with generated stream conversion methods
    public class UserEntity
    {
        public string PK { get; set; } = string.Empty;
        public string SK { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;

        public static UserEntity? FromDynamoDbStream(Dictionary<string, LambdaAttributeValue>? item)
        {
            if (item == null) return null;

            return new UserEntity
            {
                PK = item.TryGetValue("PK", out var pk) ? pk.S : string.Empty,
                SK = item.TryGetValue("SK", out var sk) ? sk.S : string.Empty,
                Name = item.TryGetValue("Name", out var name) ? name.S : string.Empty,
                EntityType = item.TryGetValue("EntityType", out var type) ? type.S : string.Empty
            };
        }

        public static UserEntity? FromStreamImage(DynamoDBEvent.StreamRecord streamRecord, bool useNewImage)
        {
            var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;
            return FromDynamoDbStream(image);
        }
    }

    public class OrderEntity
    {
        public string PK { get; set; } = string.Empty;
        public string SK { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string EntityType { get; set; } = string.Empty;

        public static OrderEntity? FromDynamoDbStream(Dictionary<string, LambdaAttributeValue>? item)
        {
            if (item == null) return null;

            return new OrderEntity
            {
                PK = item.TryGetValue("PK", out var pk) ? pk.S : string.Empty,
                SK = item.TryGetValue("SK", out var sk) ? sk.S : string.Empty,
                Total = item.TryGetValue("Total", out var total) ? decimal.Parse(total.N) : 0,
                EntityType = item.TryGetValue("EntityType", out var type) ? type.S : string.Empty
            };
        }

        public static OrderEntity? FromStreamImage(DynamoDBEvent.StreamRecord streamRecord, bool useNewImage)
        {
            var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;
            return FromDynamoDbStream(image);
        }
    }

    public class ProductEntity
    {
        public string PK { get; set; } = string.Empty;
        public string SK { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        public static ProductEntity? FromDynamoDbStream(Dictionary<string, LambdaAttributeValue>? item)
        {
            if (item == null) return null;

            return new ProductEntity
            {
                PK = item.TryGetValue("PK", out var pk) ? pk.S : string.Empty,
                SK = item.TryGetValue("SK", out var sk) ? sk.S : string.Empty,
                ProductName = item.TryGetValue("ProductName", out var name) ? name.S : string.Empty
            };
        }

        public static ProductEntity? FromStreamImage(DynamoDBEvent.StreamRecord streamRecord, bool useNewImage)
        {
            var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;
            return FromDynamoDbStream(image);
        }
    }

    private DynamoDBEvent.DynamodbStreamRecord CreateStreamRecord(
        string eventName,
        Dictionary<string, LambdaAttributeValue>? newImage = null,
        Dictionary<string, LambdaAttributeValue>? oldImage = null,
        Dictionary<string, LambdaAttributeValue>? keys = null)
    {
        return new DynamoDBEvent.DynamodbStreamRecord
        {
            EventName = eventName,
            Dynamodb = new DynamoDBEvent.StreamRecord
            {
                NewImage = newImage,
                OldImage = oldImage,
                Keys = keys ?? new Dictionary<string, LambdaAttributeValue>()
            }
        };
    }

    private Dictionary<string, LambdaAttributeValue> CreateUserImage(
        string pk = "USER#123",
        string sk = "PROFILE",
        string name = "John Doe",
        string entityType = "User")
    {
        return new Dictionary<string, LambdaAttributeValue>
        {
            ["PK"] = new LambdaAttributeValue { S = pk },
            ["SK"] = new LambdaAttributeValue { S = sk },
            ["Name"] = new LambdaAttributeValue { S = name },
            ["EntityType"] = new LambdaAttributeValue { S = entityType }
        };
    }

    private Dictionary<string, LambdaAttributeValue> CreateOrderImage(
        string pk = "USER#123",
        string sk = "ORDER#456",
        decimal total = 99.99m,
        string entityType = "Order")
    {
        return new Dictionary<string, LambdaAttributeValue>
        {
            ["PK"] = new LambdaAttributeValue { S = pk },
            ["SK"] = new LambdaAttributeValue { S = sk },
            ["Total"] = new LambdaAttributeValue { N = total.ToString() },
            ["EntityType"] = new LambdaAttributeValue { S = entityType }
        };
    }

    private Dictionary<string, LambdaAttributeValue> CreateProductImage(
        string pk = "PRODUCT#789",
        string sk = "METADATA",
        string productName = "Widget")
    {
        return new Dictionary<string, LambdaAttributeValue>
        {
            ["PK"] = new LambdaAttributeValue { S = pk },
            ["SK"] = new LambdaAttributeValue { S = sk },
            ["ProductName"] = new LambdaAttributeValue { S = productName }
        };
    }

    [Fact]
    public async Task ProcessAsync_ExactDiscriminatorMatch_ExecutesCorrectHandler()
    {
        // Arrange
        var newImage = CreateUserImage(entityType: "User");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var userHandlerExecuted = false;
        var orderHandlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    user.Name.Should().Be("John Doe");
                    await Task.CompletedTask;
                })
            .For<OrderEntity>("Order")
                .OnInsert(async (_, order) =>
                {
                    orderHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        userHandlerExecuted.Should().BeTrue();
        orderHandlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_PrefixPatternMatch_ExecutesCorrectHandler()
    {
        // Arrange
        var newImage = CreateUserImage(sk: "USER#123");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var userHandlerExecuted = false;
        UserEntity? capturedUser = null;

        // Act
        await record.Process()
            .WithDiscriminator("SK")
            .For<UserEntity>("USER#*")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    capturedUser = user;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        userHandlerExecuted.Should().BeTrue();
        capturedUser.Should().NotBeNull();
        capturedUser!.SK.Should().Be("USER#123");
    }

    [Fact]
    public async Task ProcessAsync_SuffixPatternMatch_ExecutesCorrectHandler()
    {
        // Arrange
        var newImage = CreateOrderImage(sk: "ADMIN#ORDER");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var orderHandlerExecuted = false;
        OrderEntity? capturedOrder = null;

        // Act
        await record.Process()
            .WithDiscriminator("SK")
            .For<OrderEntity>("*#ORDER")
                .OnInsert(async (_, order) =>
                {
                    orderHandlerExecuted = true;
                    capturedOrder = order;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        orderHandlerExecuted.Should().BeTrue();
        capturedOrder.Should().NotBeNull();
        capturedOrder!.SK.Should().Be("ADMIN#ORDER");
    }

    [Fact]
    public async Task ProcessAsync_ContainsPatternMatch_ExecutesCorrectHandler()
    {
        // Arrange
        var newImage = CreateUserImage(sk: "ADMIN#USER#123");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var userHandlerExecuted = false;
        UserEntity? capturedUser = null;

        // Act
        await record.Process()
            .WithDiscriminator("SK")
            .For<UserEntity>("*#USER#*")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    capturedUser = user;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        userHandlerExecuted.Should().BeTrue();
        capturedUser.Should().NotBeNull();
        capturedUser!.SK.Should().Be("ADMIN#USER#123");
    }

    [Fact]
    public async Task ProcessAsync_UnknownDiscriminatorValue_ExecutesOnUnknownTypeHandler()
    {
        // Arrange
        var newImage = CreateProductImage();
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var unknownTypeHandlerExecuted = false;
        DynamoDBEvent.DynamodbStreamRecord? capturedRecord = null;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) => await Task.CompletedTask)
            .For<OrderEntity>("Order")
                .OnInsert(async (_, order) => await Task.CompletedTask)
            .OnUnknownType(async r =>
            {
                unknownTypeHandlerExecuted = true;
                capturedRecord = r;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        unknownTypeHandlerExecuted.Should().BeTrue();
        capturedRecord.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_UnknownDiscriminatorValueWithoutHandler_SkipsSilently()
    {
        // Arrange
        var newImage = CreateProductImage();
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var userHandlerExecuted = false;

        // Act & Assert - should not throw
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        userHandlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_MissingDiscriminatorField_ExecutesOnUnknownTypeHandler()
    {
        // Arrange
        var newImage = new Dictionary<string, LambdaAttributeValue>
        {
            ["PK"] = new LambdaAttributeValue { S = "TEST#123" },
            ["SK"] = new LambdaAttributeValue { S = "METADATA" }
            // EntityType field is missing
        };
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var unknownTypeHandlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) => await Task.CompletedTask)
            .OnUnknownType(async r =>
            {
                unknownTypeHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        unknownTypeHandlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_DiscriminatorFromNewImage_InsertEvent()
    {
        // Arrange
        var newImage = CreateUserImage(entityType: "User");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) =>
                {
                    handlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_DiscriminatorFromOldImage_RemoveEvent()
    {
        // Arrange
        var oldImage = CreateUserImage(entityType: "User");
        var record = CreateStreamRecord("REMOVE", oldImage: oldImage);

        var handlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnDelete(async (user, _) =>
                {
                    handlerExecuted = true;
                    user.Name.Should().Be("John Doe");
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_FirstMatchWins_MultiplePatterns()
    {
        // Arrange
        var newImage = CreateUserImage(sk: "USER#123");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var firstHandlerExecuted = false;
        var secondHandlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("SK")
            .For<UserEntity>("USER#*")  // This should match first
                .OnInsert(async (_, user) =>
                {
                    firstHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .For<UserEntity>("*#123")  // This would also match but should not execute
                .OnInsert(async (_, user) =>
                {
                    secondHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        firstHandlerExecuted.Should().BeTrue();
        secondHandlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_MultipleEntityTypes_RoutesCorrectly()
    {
        // Arrange - User record
        var userImage = CreateUserImage(entityType: "User");
        var userRecord = CreateStreamRecord("INSERT", newImage: userImage);

        // Arrange - Order record
        var orderImage = CreateOrderImage(entityType: "Order");
        var orderRecord = CreateStreamRecord("INSERT", newImage: orderImage);

        var userHandlerExecuted = false;
        var orderHandlerExecuted = false;

        // Act - Process user record
        await userRecord.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .For<OrderEntity>("Order")
                .OnInsert(async (_, order) =>
                {
                    orderHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert - Only user handler executed
        userHandlerExecuted.Should().BeTrue();
        orderHandlerExecuted.Should().BeFalse();

        // Reset
        userHandlerExecuted = false;
        orderHandlerExecuted = false;

        // Act - Process order record
        await orderRecord.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .OnInsert(async (_, user) =>
                {
                    userHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .For<OrderEntity>("Order")
                .OnInsert(async (_, order) =>
                {
                    orderHandlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert - Only order handler executed
        userHandlerExecuted.Should().BeFalse();
        orderHandlerExecuted.Should().BeTrue();
    }

    [Fact]
    public void For_WithNullPattern_ThrowsArgumentException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            record.Process()
                .WithDiscriminator("EntityType")
                .For<UserEntity>(null!);
        });
    }

    [Fact]
    public void For_WithEmptyPattern_ThrowsArgumentException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            record.Process()
                .WithDiscriminator("EntityType")
                .For<UserEntity>("");
        });
    }

    [Fact]
    public void OnUnknownType_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            record.Process()
                .WithDiscriminator("EntityType")
                .OnUnknownType(null!);
        });
    }

    [Fact]
    public async Task ProcessAsync_WithFiltersOnRegistration_AppliesFilters()
    {
        // Arrange
        var newImage = CreateUserImage(name: "Active User", entityType: "User");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .Where(u => u.Name.Contains("Active"))
                .OnInsert(async (_, user) =>
                {
                    handlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithFailingFilter_SkipsHandler()
    {
        // Arrange
        var newImage = CreateUserImage(name: "Inactive User", entityType: "User");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process()
            .WithDiscriminator("EntityType")
            .For<UserEntity>("User")
                .Where(u => u.Name.Contains("Active"))
                .OnInsert(async (_, user) =>
                {
                    handlerExecuted = true;
                    await Task.CompletedTask;
                })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeFalse();
    }

    // Registry lookup tests

    [Fact]
    public void For_WithoutRegistry_ThrowsInvalidOperationException()
    {
        // Arrange
        var record = CreateStreamRecord(
            "INSERT",
            newImage: new Dictionary<string, LambdaAttributeValue>
            {
                ["PK"] = new LambdaAttributeValue { S = "USER#123" },
                ["SK"] = new LambdaAttributeValue { S = "USER#123" },
                ["EntityType"] = new LambdaAttributeValue { S = "User" },
                ["Name"] = new LambdaAttributeValue { S = "John Doe" }
            });

        var builder = record.Process().WithDiscriminator("EntityType");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.For<UserEntity>());
        exception.Message.Should().Contain("without discriminator registry");
        exception.Message.Should().Contain("table.OnStream(record)");
    }

    // NOTE: Tests for WithRegistry() have been removed because WithRegistry is an internal API
    // used by the source generator. Those tests were testing internal implementation details
    // that are not part of the public API surface.
}
