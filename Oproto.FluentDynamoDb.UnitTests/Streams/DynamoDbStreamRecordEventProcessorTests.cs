using Amazon.Lambda.DynamoDBEvents;
using FluentAssertions;
using Oproto.FluentDynamoDb.Streams;

namespace Oproto.FluentDynamoDb.UnitTests.Streams;

public class DynamoDbStreamRecordEventProcessorTests
{
    [Fact]
    public async Task OnInsertTestAsync()
    {
        var record = new DynamoDBEvent.DynamodbStreamRecord()
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
        
        var processor = new DynamoDbStreamRecordEventProcessor(record);
        await processor.Awaitable()
            .OnInsert(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }))
            .OnUpdate(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnDelete(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnNonTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }));
    }
    
    [Fact]
    public async Task OnUpdateTestAsync()
    {
        var record = new DynamoDBEvent.DynamodbStreamRecord()
        {
            EventName = "UPDATE",
            Dynamodb = new DynamoDBEvent.StreamRecord()
            {

            },
            UserIdentity = new()
            {
                PrincipalId = "",
                Type = ""
            }
        };
        
        var processor = new DynamoDbStreamRecordEventProcessor(record);
        await processor.Awaitable()
            .OnInsert(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnUpdate(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }))
            .OnDelete(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnNonTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }));
    }
    
    [Fact]
    public async Task OnDeleteTestAsync()
    {
        var record = new DynamoDBEvent.DynamodbStreamRecord()
        {
            EventName = "REMOVE",
            Dynamodb = new DynamoDBEvent.StreamRecord()
            {

            },
            UserIdentity = new()
            {
                PrincipalId = "",
                Type = ""
            }
        };
        
        var processor = new DynamoDbStreamRecordEventProcessor(record);
        await processor.Awaitable()
            .OnInsert(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnUpdate(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnDelete(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }))
            .OnNonTtlDelete(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }))
            .OnTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }));
    }
    
    [Fact]
    public async Task OnDeleteWithTtlTestAsync()
    {
        var record = new DynamoDBEvent.DynamodbStreamRecord()
        {
            EventName = "REMOVE",
            Dynamodb = new DynamoDBEvent.StreamRecord()
            {

            },
            UserIdentity = new()
            {
                PrincipalId = "dynamodb.amazonaws.com",
                Type = "Service"
            }
        };
        
        var processor = new DynamoDbStreamRecordEventProcessor(record);
        await processor.Awaitable()
            .OnInsert(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnUpdate(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnDelete(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }))
            .OnNonTtlDelete(async envt => await Task.Run(() => { Assert.Fail(""); }))
            .OnTtlDelete(async envt => await Task.Run(() => { envt.Should().NotBeNull(); }));
    }
}