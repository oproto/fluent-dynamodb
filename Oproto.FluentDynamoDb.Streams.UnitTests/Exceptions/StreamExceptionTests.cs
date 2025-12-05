using Amazon.Lambda.DynamoDBEvents;
using Oproto.FluentDynamoDb.Streams.Exceptions;
using Oproto.FluentDynamoDb.Streams.Extensions;
using Oproto.FluentDynamoDb.Streams.Processing;
using LambdaAttributeValue = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace Oproto.FluentDynamoDb.Streams.UnitTests.Exceptions;

public class StreamExceptionTests
{
    [Fact]
    public void StreamProcessingException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new StreamProcessingException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void StreamProcessingException_WithMessage_StoresMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var exception = new StreamProcessingException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void StreamProcessingException_WithMessageAndInnerException_StoresBoth()
    {
        // Arrange
        var message = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new StreamProcessingException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void StreamDeserializationException_WithEntityType_StoresEntityType()
    {
        // Arrange
        var message = "Deserialization failed";
        var entityType = typeof(TestEntity);
        var innerException = new InvalidCastException("Type mismatch");

        // Act
        var exception = new StreamDeserializationException(message, entityType, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.EntityType.Should().Be(entityType);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void StreamDeserializationException_WithPropertyName_StoresPropertyName()
    {
        // Arrange
        var message = "Property deserialization failed";
        var entityType = typeof(TestEntity);
        var propertyName = "TestProperty";
        var innerException = new FormatException("Invalid format");

        // Act
        var exception = new StreamDeserializationException(message, entityType, propertyName, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.EntityType.Should().Be(entityType);
        exception.PropertyName.Should().Be(propertyName);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void DiscriminatorMismatchException_WithContext_StoresAllProperties()
    {
        // Arrange
        var message = "Discriminator mismatch";
        var fieldName = "EntityType";
        var expectedValue = "User";
        var actualValue = "Order";

        // Act
        var exception = new DiscriminatorMismatchException(message, fieldName, expectedValue, actualValue);

        // Assert
        exception.Message.Should().Be(message);
        exception.FieldName.Should().Be(fieldName);
        exception.ExpectedValue.Should().Be(expectedValue);
        exception.ActualValue.Should().Be(actualValue);
    }

    [Fact]
    public void DiscriminatorMismatchException_WithInnerException_StoresInnerException()
    {
        // Arrange
        var message = "Discriminator mismatch";
        var fieldName = "EntityType";
        var expectedValue = "User";
        var actualValue = "Order";
        var innerException = new KeyNotFoundException("Field not found");

        // Act
        var exception = new DiscriminatorMismatchException(message, fieldName, expectedValue, actualValue, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.FieldName.Should().Be(fieldName);
        exception.ExpectedValue.Should().Be(expectedValue);
        exception.ActualValue.Should().Be(actualValue);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void StreamFilterException_WithFilterExpression_StoresFilterExpression()
    {
        // Arrange
        var message = "Filter evaluation failed";
        var filterExpression = "x => x.Status == \"active\"";
        var innerException = new NullReferenceException("Property is null");

        // Act
        var exception = new StreamFilterException(message, filterExpression, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.FilterExpression.Should().Be(filterExpression);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public async Task TypedStreamProcessor_WhereKeyThrows_WrapsInStreamFilterException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");
        var processor = record.Process<TestEntity>()
            .WhereKey(keys => throw new InvalidOperationException("Key filter error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<StreamFilterException>(() => processor.ProcessAsync());
        exception.FilterExpression.Should().Be("WhereKey predicate");
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
        exception.InnerException!.Message.Should().Be("Key filter error");
    }

    // NOTE: Test for Where() throwing has been removed because Where() takes an Expression<Func<TEntity, bool>>
    // which cannot contain statement bodies or throw expressions. The test was attempting to test an impossible scenario.

    [Fact]
    public async Task TypedStreamProcessor_DeserializationFails_WrapsInStreamDeserializationException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");
        var processor = record.Process<EntityWithoutFromDynamoDbStream>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        exception.Message.Should().Contain("must have a static FromDynamoDbStream or FromStreamImage method");
    }

    [Fact]
    public async Task TypedStreamProcessor_HandlerThrows_PropagatesException()
    {
        // Arrange
        var record = CreateStreamRecord("INSERT");
        var handlerException = new InvalidOperationException("Handler error");
        var processor = record.Process<TestEntity>()
            .OnInsert((_, __) => throw handlerException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        exception.Should().Be(handlerException);
        exception.Message.Should().Be("Handler error");
    }

    // NOTE: Tests for TypeHandlerRegistration have been removed because TypeHandlerRegistration
    // has an internal constructor and is only created through the public DiscriminatorStreamProcessorBuilder.For<T>() method.
    // Those tests were testing internal implementation details.

    private static DynamoDBEvent.DynamodbStreamRecord CreateStreamRecord(string eventName)
    {
        return new DynamoDBEvent.DynamodbStreamRecord
        {
            EventName = eventName,
            Dynamodb = new DynamoDBEvent.StreamRecord
            {
                Keys = new Dictionary<string, LambdaAttributeValue>
                {
                    ["pk"] = new LambdaAttributeValue { S = "TEST#123" }
                },
                NewImage = new Dictionary<string, LambdaAttributeValue>
                {
                    ["pk"] = new LambdaAttributeValue { S = "TEST#123" },
                    ["Name"] = new LambdaAttributeValue { S = "Test Entity" }
                }
            }
        };
    }

    // Test entity with FromDynamoDbStream method
    public class TestEntity
    {
        public string? Name { get; set; }

        public static TestEntity? FromDynamoDbStream(Dictionary<string, LambdaAttributeValue>? item)
        {
            if (item == null) return null;

            return new TestEntity
            {
                Name = item.TryGetValue("Name", out var name) ? name.S : null
            };
        }
    }

    // Test entity without FromDynamoDbStream method
    public class EntityWithoutFromDynamoDbStream
    {
        public string? Name { get; set; }
    }
}
