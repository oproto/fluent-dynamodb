using AwesomeAssertions;
using Oproto.FluentDynamoDb.Streams.Extensions;
using Oproto.FluentDynamoDb.Streams.Processing;
using LambdaAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace Oproto.FluentDynamoDb.Streams.UnitTests.Processing;

public class TypedStreamProcessorTests
{
    // Test entity with generated stream conversion methods
    public class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Value { get; set; }

        // Simulated generated method
        public static TestEntity? FromDynamoDbStream(Dictionary<string, LambdaAttributeValue>? item)
        {
            if (item == null) return null;

            return new TestEntity
            {
                Id = item.TryGetValue("Id", out var id) ? id.S : string.Empty,
                Name = item.TryGetValue("Name", out var name) ? name.S : string.Empty,
                Status = item.TryGetValue("Status", out var status) ? status.S : string.Empty,
                Value = item.TryGetValue("Value", out var value) ? int.Parse(value.N) : 0
            };
        }

        public static TestEntity? FromStreamImage(DynamoDBEvent.StreamRecord streamRecord, bool useNewImage)
        {
            var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;
            return FromDynamoDbStream(image);
        }
    }

    private DynamoDBEvent.DynamodbStreamRecord CreateStreamRecord(
        string eventName,
        Dictionary<string, LambdaAttributeValue>? newImage = null,
        Dictionary<string, LambdaAttributeValue>? oldImage = null,
        Dictionary<string, LambdaAttributeValue>? keys = null,
        DynamoDBEvent.Identity? userIdentity = null)
    {
        return new DynamoDBEvent.DynamodbStreamRecord
        {
            EventName = eventName,
            Dynamodb = new DynamoDBEvent.StreamRecord
            {
                NewImage = newImage,
                OldImage = oldImage,
                Keys = keys ?? new Dictionary<string, LambdaAttributeValue>()
            },
            UserIdentity = userIdentity
        };
    }

    private Dictionary<string, LambdaAttributeValue> CreateTestImage(
        string id = "test-id",
        string name = "Test Name",
        string status = "active",
        int value = 100)
    {
        return new Dictionary<string, LambdaAttributeValue>
        {
            ["Id"] = new LambdaAttributeValue { S = id },
            ["Name"] = new LambdaAttributeValue { S = name },
            ["Status"] = new LambdaAttributeValue { S = status },
            ["Value"] = new LambdaAttributeValue { N = value.ToString() }
        };
    }

    [Fact]
    public async Task ProcessAsync_InsertEvent_ExecutesOnInsertHandler()
    {
        // Arrange
        var newImage = CreateTestImage();
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;
        TestEntity? capturedOldEntity = null;
        TestEntity? capturedNewEntity = null;

        // Act
        await record.Process<TestEntity>()
            .OnInsert(async (oldEntity, newEntity) =>
            {
                handlerExecuted = true;
                capturedOldEntity = oldEntity;
                capturedNewEntity = newEntity;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
        capturedOldEntity.Should().BeNull();
        capturedNewEntity.Should().NotBeNull();
        capturedNewEntity!.Id.Should().Be("test-id");
        capturedNewEntity.Name.Should().Be("Test Name");
    }

    [Fact]
    public async Task ProcessAsync_ModifyEvent_ExecutesOnUpdateHandler()
    {
        // Arrange
        var oldImage = CreateTestImage(name: "Old Name", value: 50);
        var newImage = CreateTestImage(name: "New Name", value: 100);
        var record = CreateStreamRecord("MODIFY", newImage: newImage, oldImage: oldImage);

        var handlerExecuted = false;
        TestEntity? capturedOldEntity = null;
        TestEntity? capturedNewEntity = null;

        // Act
        await record.Process<TestEntity>()
            .OnUpdate(async (oldEntity, newEntity) =>
            {
                handlerExecuted = true;
                capturedOldEntity = oldEntity;
                capturedNewEntity = newEntity;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
        capturedOldEntity.Should().NotBeNull();
        capturedNewEntity.Should().NotBeNull();
        capturedOldEntity!.Name.Should().Be("Old Name");
        capturedOldEntity.Value.Should().Be(50);
        capturedNewEntity!.Name.Should().Be("New Name");
        capturedNewEntity.Value.Should().Be(100);
    }

    [Fact]
    public async Task ProcessAsync_RemoveEvent_ExecutesOnDeleteHandler()
    {
        // Arrange
        var oldImage = CreateTestImage();
        var record = CreateStreamRecord("REMOVE", oldImage: oldImage);

        var handlerExecuted = false;
        TestEntity? capturedOldEntity = null;
        TestEntity? capturedNewEntity = null;

        // Act
        await record.Process<TestEntity>()
            .OnDelete(async (oldEntity, newEntity) =>
            {
                handlerExecuted = true;
                capturedOldEntity = oldEntity;
                capturedNewEntity = newEntity;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
        capturedOldEntity.Should().NotBeNull();
        capturedNewEntity.Should().BeNull();
        capturedOldEntity!.Id.Should().Be("test-id");
    }

    [Fact]
    public async Task ProcessAsync_TtlDelete_ExecutesOnTtlDeleteHandler()
    {
        // Arrange
        var oldImage = CreateTestImage();
        var userIdentity = new DynamoDBEvent.Identity { Type = "Service", PrincipalId = "dynamodb.amazonaws.com" };
        var record = CreateStreamRecord("REMOVE", oldImage: oldImage, userIdentity: userIdentity);

        var ttlHandlerExecuted = false;
        var nonTtlHandlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .OnTtlDelete(async (oldEntity, _) =>
            {
                ttlHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .OnNonTtlDelete(async (oldEntity, _) =>
            {
                nonTtlHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        ttlHandlerExecuted.Should().BeTrue();
        nonTtlHandlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_NonTtlDelete_ExecutesOnNonTtlDeleteHandler()
    {
        // Arrange
        var oldImage = CreateTestImage();
        var record = CreateStreamRecord("REMOVE", oldImage: oldImage);

        var ttlHandlerExecuted = false;
        var nonTtlHandlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .OnTtlDelete(async (oldEntity, _) =>
            {
                ttlHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .OnNonTtlDelete(async (oldEntity, _) =>
            {
                nonTtlHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        ttlHandlerExecuted.Should().BeFalse();
        nonTtlHandlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_RemoveEvent_ExecutesBothOnDeleteAndTtlSpecificHandlers()
    {
        // Arrange
        var oldImage = CreateTestImage();
        var userIdentity = new DynamoDBEvent.Identity { Type = "Service", PrincipalId = "dynamodb.amazonaws.com" };
        var record = CreateStreamRecord("REMOVE", oldImage: oldImage, userIdentity: userIdentity);

        var deleteHandlerExecuted = false;
        var ttlHandlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .OnDelete(async (oldEntity, _) =>
            {
                deleteHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .OnTtlDelete(async (oldEntity, _) =>
            {
                ttlHandlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        deleteHandlerExecuted.Should().BeTrue();
        ttlHandlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task Where_FilterReturnsTrue_ExecutesHandler()
    {
        // Arrange
        var newImage = CreateTestImage(status: "active");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .Where(e => e.Status == "active")
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task Where_FilterReturnsFalse_SkipsHandler()
    {
        // Arrange
        var newImage = CreateTestImage(status: "inactive");
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .Where(e => e.Status == "active")
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task Where_MultipleFilters_AppliesAndLogic()
    {
        // Arrange
        var newImage = CreateTestImage(status: "active", value: 150);
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .Where(e => e.Status == "active")
            .Where(e => e.Value > 100)
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task Where_MultipleFiltersOneFails_SkipsHandler()
    {
        // Arrange
        var newImage = CreateTestImage(status: "active", value: 50);
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .Where(e => e.Status == "active")
            .Where(e => e.Value > 100)
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task WhereKey_FilterReturnsTrue_ExecutesHandler()
    {
        // Arrange
        var newImage = CreateTestImage();
        var keys = new Dictionary<string, LambdaAttributeValue>
        {
            ["pk"] = new LambdaAttributeValue { S = "USER#123" }
        };
        var record = CreateStreamRecord("INSERT", newImage: newImage, keys: keys);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .WhereKey(k => k["pk"].S.StartsWith("USER#"))
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task WhereKey_FilterReturnsFalse_SkipsHandlerAndDeserialization()
    {
        // Arrange
        var newImage = CreateTestImage();
        var keys = new Dictionary<string, LambdaAttributeValue>
        {
            ["pk"] = new LambdaAttributeValue { S = "ORDER#123" }
        };
        var record = CreateStreamRecord("INSERT", newImage: newImage, keys: keys);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .WhereKey(k => k["pk"].S.StartsWith("USER#"))
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_MultipleHandlersSameType_ExecutesInOrder()
    {
        // Arrange
        var newImage = CreateTestImage();
        var record = CreateStreamRecord("INSERT", newImage: newImage);

        var executionOrder = new List<int>();

        // Act
        await record.Process<TestEntity>()
            .OnInsert(async (_, entity) =>
            {
                executionOrder.Add(1);
                await Task.CompletedTask;
            })
            .OnInsert(async (_, entity) =>
            {
                executionOrder.Add(2);
                await Task.CompletedTask;
            })
            .OnInsert(async (_, entity) =>
            {
                executionOrder.Add(3);
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        executionOrder.Should().Equal(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Where_ReturnsNewInstance_PreservesImmutability()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT", newImage: CreateTestImage());
        var processor1 = record.Process<TestEntity>();

        // Act
        var processor2 = processor1.Where(e => e.Status == "active");
        var processor3 = processor1.Where(e => e.Value > 100);

        // Assert
        processor2.Should().NotBeSameAs(processor1);
        processor3.Should().NotBeSameAs(processor1);
        processor3.Should().NotBeSameAs(processor2);
    }

    [Fact]
    public void OnInsert_ReturnsNewInstance_PreservesImmutability()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT", newImage: CreateTestImage());
        var processor1 = record.Process<TestEntity>();

        // Act
        var processor2 = processor1.OnInsert(async (_, e) => await Task.CompletedTask);
        var processor3 = processor1.OnInsert(async (_, e) => await Task.CompletedTask);

        // Assert
        processor2.Should().NotBeSameAs(processor1);
        processor3.Should().NotBeSameAs(processor1);
        processor3.Should().NotBeSameAs(processor2);
    }

    [Fact]
    public async Task ProcessAsync_CombinedWhereKeyAndWhere_EvaluatesInCorrectOrder()
    {
        // Arrange
        var newImage = CreateTestImage(status: "active", value: 150);
        var keys = new Dictionary<string, LambdaAttributeValue>
        {
            ["pk"] = new LambdaAttributeValue { S = "USER#123" }
        };
        var record = CreateStreamRecord("INSERT", newImage: newImage, keys: keys);

        var handlerExecuted = false;

        // Act
        await record.Process<TestEntity>()
            .WhereKey(k => k["pk"].S.StartsWith("USER#"))
            .Where(e => e.Status == "active")
            .Where(e => e.Value > 100)
            .OnInsert(async (_, entity) =>
            {
                handlerExecuted = true;
                await Task.CompletedTask;
            })
            .ProcessAsync();

        // Assert
        handlerExecuted.Should().BeTrue();
    }

    [Fact]
    public void Where_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT", newImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.Where(null!));
        exception.ParamName.Should().Be("predicate");
    }

    [Fact]
    public void WhereKey_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT", newImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.WhereKey(null!));
        exception.ParamName.Should().Be("predicate");
    }

    [Fact]
    public void OnInsert_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT", newImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.OnInsert(null!));
        exception.ParamName.Should().Be("handler");
    }

    [Fact]
    public void OnUpdate_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("MODIFY", newImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.OnUpdate(null!));
        exception.ParamName.Should().Be("handler");
    }

    [Fact]
    public void OnDelete_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("REMOVE", oldImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.OnDelete(null!));
        exception.ParamName.Should().Be("handler");
    }

    [Fact]
    public void OnTtlDelete_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("REMOVE", oldImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.OnTtlDelete(null!));
        exception.ParamName.Should().Be("handler");
    }

    [Fact]
    public void OnNonTtlDelete_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var record = CreateStreamRecord("REMOVE", oldImage: CreateTestImage());
        var processor = record.Process<TestEntity>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => processor.OnNonTtlDelete(null!));
        exception.ParamName.Should().Be("handler");
    }
}
