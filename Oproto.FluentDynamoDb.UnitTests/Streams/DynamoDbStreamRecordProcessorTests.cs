using System.Text.RegularExpressions;
using Amazon.Lambda.DynamoDBEvents;
using FluentAssertions;
using Oproto.FluentDynamoDb.Streams;

namespace Oproto.FluentDynamoDb.UnitTests.Streams;

public class DynamoDbStreamRecordProcessorTests
{
    public DynamoDBEvent.DynamodbStreamRecord TestRecord1()
    {
        return new DynamoDBEvent.DynamodbStreamRecord()
        {
            EventName = "INSERT",
            Dynamodb = new DynamoDBEvent.StreamRecord()
            {
                Keys = new Dictionary<string, DynamoDBEvent.AttributeValue>()
                {
                    { "pk", new DynamoDBEvent.AttributeValue() { S = "1234" } },
                    { "sk", new DynamoDBEvent.AttributeValue() { S = "foo#1" } }
                }
            },
            UserIdentity = new()
            {
                PrincipalId = "",
                Type = ""
            }
        };
    }
    
    [Fact]
    public async Task OnMatchPrimaryKeyOnlySuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnMatch("pk", "1234", processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnMatchPrimaryKeyOnlyFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnMatch("pk", "5243", processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    [Fact]
    public async Task OnMatchPrimaryKeySortKeySuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnMatch("pk", "1234", "sk", "foo#1", processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnMatchPrimaryKeySortKeyFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnMatch("pk", "5243", "sk", "foo#2", processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    [Fact]
    public async Task OnSortKeyMatchSuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnSortKeyMatch("sk", "foo#1", processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnSortKeyMatchFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnSortKeyMatch("sk", "foo#2", processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    [Fact]
    public async Task OnPatternMatchPrimaryKeySortKeyRegexSuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", "1234", "sk", new Regex(@"^foo\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnPatternMatchPrimaryKeySortKeyRegexFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", "5243", "sk", new Regex(@"^bar\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    
    [Fact]
    public async Task OnSortKeyPatternMatchRegexSuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnSortKeyPatternMatch("sk", new Regex(@"^foo\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnSortKeyPatternMatchRegexFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnSortKeyPatternMatch("sk", new Regex(@"^bar\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    [Fact]
    public async Task OnPatternPrimaryKeyRegexSuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", new Regex(@"^[0-9]*$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnPatternPrimaryKeyRegexFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", new Regex(@"^[a-z]*$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
    
    [Fact]
    public async Task OnPatternMatchDualRegexSuccess()
    {
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        bool didProcess = false;
        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", new Regex(@"^[0-9]*$"), "sk", new Regex(@"^foo\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { didProcess = true; }); }));
        didProcess.Should().BeTrue();
    }
    
    [Fact]
    public async Task OnPatternMatchDualRegexFail()
    {
        
        var record = TestRecord1();

        var recordProcessor = new DynamoDbStreamRecordProcessor(record);

        await recordProcessor.Awaitable()
            .OnPatternMatch("pk", new Regex(@"^[0-9]*$"), "sk", new Regex(@"^bar\#[0-9]?$"), processor => processor.OnInsert(async (record) => { await Task.Run(() => { Assert.Fail(""); }); }));
    }
}