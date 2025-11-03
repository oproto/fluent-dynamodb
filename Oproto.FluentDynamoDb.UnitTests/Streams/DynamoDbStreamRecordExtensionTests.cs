using Amazon.Lambda.DynamoDBEvents;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Streams;

namespace Oproto.FluentDynamoDb.UnitTests.Streams;

public class DynamoDbStreamRecordExtensionTests
{
    public DynamoDBEvent.DynamodbStreamRecord TestRecord() => new DynamoDBEvent.DynamodbStreamRecord()
    {
        EventName = "INSERT",
        Dynamodb = new DynamoDBEvent.StreamRecord()
        {

        },
        UserIdentity = new()
        {
            PrincipalId = "",
            Type = ""
        }
    };

    [Fact]
    public async Task ProcessReturnsSuccessfully()
    {
        var record = TestRecord();
        var processor = await record.Process();
        processor.Should().NotBeNull();
        processor.Record.Should().NotBeNull();
    }
}