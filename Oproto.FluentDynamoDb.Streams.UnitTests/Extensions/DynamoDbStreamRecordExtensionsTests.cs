using Oproto.FluentDynamoDb.Streams.Extensions;
using Oproto.FluentDynamoDb.Streams.Processing;

namespace Oproto.FluentDynamoDb.Streams.UnitTests.Extensions;

public class DynamoDbStreamRecordExtensionsTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Process_Generic_ReturnsTypedStreamProcessor()
    {
        // Arrange
        var record = CreateStreamRecord();

        // Act - Call the new extension method from Oproto.FluentDynamoDb.Streams
        var result = Oproto.FluentDynamoDb.Streams.Extensions.DynamoDbStreamRecordExtensions.Process<TestEntity>(record);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TypedStreamProcessor<TestEntity>>(result);
    }

    [Fact]
    public void Process_NonGeneric_ReturnsStreamRecordProcessorBuilder()
    {
        // Arrange
        var record = CreateStreamRecord();

        // Act - Call the new extension method from Oproto.FluentDynamoDb.Streams
        var result = Oproto.FluentDynamoDb.Streams.Extensions.DynamoDbStreamRecordExtensions.Process(record);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<StreamRecordProcessorBuilder>(result);
    }

    private static DynamoDBEvent.DynamodbStreamRecord CreateStreamRecord()
    {
        return new DynamoDBEvent.DynamodbStreamRecord
        {
            EventName = "INSERT",
            Dynamodb = new DynamoDBEvent.StreamRecord
            {
                Keys = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    ["pk"] = new DynamoDBEvent.AttributeValue { S = "TEST#123" }
                },
                NewImage = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    ["pk"] = new DynamoDBEvent.AttributeValue { S = "TEST#123" },
                    ["name"] = new DynamoDBEvent.AttributeValue { S = "Test Name" }
                }
            }
        };
    }
}
