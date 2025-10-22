using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Integration tests for request builder logging functionality.
/// Tests verify that logging is properly integrated into request builders.
/// </summary>
[Trait("Category", "Integration")]
public class RequestBuilderLoggingTests
{
    private class TestLogger : IDynamoDbLogger
    {
        private readonly List<LogEntry> _logEntries = new();
        private readonly LogLevel _minimumLevel;

        public TestLogger(LogLevel minimumLevel = LogLevel.Trace)
        {
            _minimumLevel = minimumLevel;
        }

        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void LogTrace(int eventId, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Trace))
            {
                _logEntries.Add(new LogEntry(LogLevel.Trace, eventId, message, args, null));
            }
        }

        public void LogDebug(int eventId, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Debug))
            {
                _logEntries.Add(new LogEntry(LogLevel.Debug, eventId, message, args, null));
            }
        }

        public void LogInformation(int eventId, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Information))
            {
                _logEntries.Add(new LogEntry(LogLevel.Information, eventId, message, args, null));
            }
        }

        public void LogWarning(int eventId, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Warning))
            {
                _logEntries.Add(new LogEntry(LogLevel.Warning, eventId, message, args, null));
            }
        }

        public void LogError(int eventId, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Error))
            {
                _logEntries.Add(new LogEntry(LogLevel.Error, eventId, message, args, null));
            }
        }

        public void LogError(int eventId, Exception exception, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Error))
            {
                _logEntries.Add(new LogEntry(LogLevel.Error, eventId, message, args, exception));
            }
        }

        public void LogCritical(int eventId, Exception exception, string message, params object[] args)
        {
            if (IsEnabled(LogLevel.Critical))
            {
                _logEntries.Add(new LogEntry(LogLevel.Critical, eventId, message, args, exception));
            }
        }

        public void Clear() => _logEntries.Clear();

        public bool HasLogEntry(LogLevel level, int eventId) =>
            _logEntries.Any(e => e.Level == level && e.EventId == eventId);

        public bool HasLogEntryContaining(string messageFragment) =>
            _logEntries.Any(e => e.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase));

        public LogEntry? GetLogEntry(LogLevel level, int eventId) =>
            _logEntries.FirstOrDefault(e => e.Level == level && e.EventId == eventId);
    }

    private class LogEntry
    {
        public LogEntry(LogLevel level, int eventId, string message, object[] args, Exception? exception)
        {
            Level = level;
            EventId = eventId;
            Message = message;
            Args = args;
            Exception = exception;
        }

        public LogLevel Level { get; }
        public int EventId { get; }
        public string Message { get; }
        public object[] Args { get; }
        public Exception? Exception { get; }
    }

    #region QueryRequestBuilder Tests

    [Fact]
    public async Task QueryRequestBuilder_LogsOperationStart()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Count = 5, ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.5 } });

        var builder = new QueryRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.ExecutingQuery).Should().BeTrue();
        var entry = logger.GetLogEntry(LogLevel.Information, LogEventIds.ExecutingQuery);
        entry.Should().NotBeNull();
        entry!.Message.Should().Contain("Executing Query");
        entry.Args.Should().Contain("TestTable");
    }

    [Fact]
    public async Task QueryRequestBuilder_LogsParametersAtTraceLevel()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Count = 5, ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.5 } });

        var builder = new QueryRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .WithValue(":sk", "test-sort")
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Trace, LogEventIds.ExecutingQuery).Should().BeTrue();
        var entry = logger.GetLogEntry(LogLevel.Trace, LogEventIds.ExecutingQuery);
        entry.Should().NotBeNull();
        entry!.Message.Should().Contain("parameters");
    }

    [Fact]
    public async Task QueryRequestBuilder_LogsOperationCompletion()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Count = 5, ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.5 } });

        var builder = new QueryRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.OperationComplete).Should().BeTrue();
        var entry = logger.GetLogEntry(LogLevel.Information, LogEventIds.OperationComplete);
        entry.Should().NotBeNull();
        entry!.Message.Should().Contain("completed");
    }

    [Fact]
    public async Task QueryRequestBuilder_LogsConsumedCapacity()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Count = 5, ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.5 } });

        var builder = new QueryRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .ExecuteAsync();

        // Assert
        var entry = logger.GetLogEntry(LogLevel.Information, LogEventIds.OperationComplete);
        entry.Should().NotBeNull();
        entry!.Args.Should().Contain(2.5);
    }

    [Fact]
    public async Task QueryRequestBuilder_LogsErrors()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedException = new ResourceNotFoundException("Table not found");
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns<QueryResponse>(_ => throw expectedException);

        var builder = new QueryRequestBuilder(mockClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .ExecuteAsync());
        
        // Assert
        logger.HasLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError).Should().BeTrue();
        var entry = logger.GetLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError);
        entry.Should().NotBeNull();
        entry!.Exception.Should().Be(expectedException);
        entry.Message.Should().Contain("failed");
    }

    #endregion

    #region GetItemRequestBuilder Tests

    [Fact]
    public async Task GetItemRequestBuilder_LogsOperation()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse 
            { 
                Item = new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = "test" } },
                ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
            });

        var builder = new GetItemRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .WithKey("id", "test-id")
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.ExecutingGetItem).Should().BeTrue();
        logger.HasLogEntry(LogLevel.Information, LogEventIds.OperationComplete).Should().BeTrue();
    }

    [Fact]
    public async Task GetItemRequestBuilder_LogsError()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedException = new ResourceNotFoundException("Table not found");
        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetItemResponse>(_ => throw expectedException);

        var builder = new GetItemRequestBuilder(mockClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await builder
            .ForTable("TestTable")
            .WithKey("id", "test-id")
            .ExecuteAsync());
        
        // Assert
        logger.HasLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError).Should().BeTrue();
    }

    #endregion

    #region PutItemRequestBuilder Tests

    [Fact]
    public async Task PutItemRequestBuilder_LogsOperation()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse { ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 } });

        var builder = new PutItemRequestBuilder(mockClient, logger);
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" }
        };

        // Act
        await builder
            .ForTable("TestTable")
            .WithItem(item)
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.ExecutingPutItem).Should().BeTrue();
        logger.HasLogEntry(LogLevel.Information, LogEventIds.OperationComplete).Should().BeTrue();
    }

    [Fact]
    public async Task PutItemRequestBuilder_LogsError()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedException = new ConditionalCheckFailedException("Condition failed");
        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<PutItemResponse>(_ => throw expectedException);

        var builder = new PutItemRequestBuilder(mockClient, logger);
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "test-id" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ConditionalCheckFailedException>(async () => await builder
            .ForTable("TestTable")
            .WithItem(item)
            .ExecuteAsync());
        
        // Assert
        logger.HasLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError).Should().BeTrue();
    }

    #endregion

    #region UpdateItemRequestBuilder Tests

    [Fact]
    public async Task UpdateItemRequestBuilder_LogsOperation()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse { ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 } });

        var builder = new UpdateItemRequestBuilder(mockClient, logger);

        // Act
        await builder
            .ForTable("TestTable")
            .WithKey("id", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated Name")
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.ExecutingUpdate).Should().BeTrue();
        logger.HasLogEntry(LogLevel.Information, LogEventIds.OperationComplete).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateItemRequestBuilder_LogsError()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedException = new ResourceNotFoundException("Table not found");
        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<UpdateItemResponse>(_ => throw expectedException);

        var builder = new UpdateItemRequestBuilder(mockClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await builder
            .ForTable("TestTable")
            .WithKey("id", "test-id")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Updated Name")
            .ExecuteAsync());
        
        // Assert
        logger.HasLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError).Should().BeTrue();
    }

    #endregion

    #region TransactWriteItemsRequestBuilder Tests

    [Fact]
    public async Task TransactWriteItemsRequestBuilder_LogsOperation()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse 
            { 
                ConsumedCapacity = new List<ConsumedCapacity>
                {
                    new ConsumedCapacity { CapacityUnits = 2.0 }
                }
            });

        var builder = new TransactWriteItemsRequestBuilder(mockClient, logger);

        // Act
        await builder
            .AddTransactItem(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = "TestTable",
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["id"] = new AttributeValue { S = "test-id" }
                    }
                }
            })
            .ExecuteAsync();

        // Assert
        logger.HasLogEntry(LogLevel.Information, LogEventIds.ExecutingTransaction).Should().BeTrue();
        logger.HasLogEntry(LogLevel.Information, LogEventIds.OperationComplete).Should().BeTrue();
    }

    [Fact]
    public async Task TransactWriteItemsRequestBuilder_LogsError()
    {
        // Arrange
        var logger = new TestLogger();
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedException = new TransactionCanceledException("Transaction canceled");
        mockClient.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw expectedException);

        var builder = new TransactWriteItemsRequestBuilder(mockClient, logger);

        // Act & Assert
        await Assert.ThrowsAsync<TransactionCanceledException>(async () => await builder
            .AddTransactItem(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = "TestTable",
                    Item = new Dictionary<string, AttributeValue>
                    {
                        ["id"] = new AttributeValue { S = "test-id" }
                    }
                }
            })
            .ExecuteAsync());
        
        // Assert
        logger.HasLogEntry(LogLevel.Error, LogEventIds.DynamoDbOperationError).Should().BeTrue();
    }

    #endregion
}
