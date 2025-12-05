using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Logging.Extensions;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using DynamoDbLogLevel = Oproto.FluentDynamoDb.Logging.LogLevel;

namespace Oproto.FluentDynamoDb.Logging.Extensions.UnitTests;

public class MicrosoftExtensionsLoggingAdapterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MicrosoftExtensionsLoggingAdapter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_DoesNotThrow()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();

        // Act
        var act = () => new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Log Level Mapping Tests (Subtask 10.1)

    [Theory]
    [InlineData(DynamoDbLogLevel.Trace, MelLogLevel.Trace)]
    [InlineData(DynamoDbLogLevel.Debug, MelLogLevel.Debug)]
    [InlineData(DynamoDbLogLevel.Information, MelLogLevel.Information)]
    [InlineData(DynamoDbLogLevel.Warning, MelLogLevel.Warning)]
    [InlineData(DynamoDbLogLevel.Error, MelLogLevel.Error)]
    [InlineData(DynamoDbLogLevel.Critical, MelLogLevel.Critical)]
    [InlineData(DynamoDbLogLevel.None, MelLogLevel.None)]
    public void IsEnabled_MapsLogLevelsCorrectly(DynamoDbLogLevel dynamoDbLevel, MelLogLevel expectedMelLevel)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(expectedMelLevel).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        var result = adapter.IsEnabled(dynamoDbLevel);

        // Assert
        result.Should().BeTrue();
        mockLogger.Received(1).IsEnabled(expectedMelLevel);
    }

    [Fact]
    public void IsEnabled_DelegatesToUnderlyingLogger_WhenEnabled()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Debug).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        var result = adapter.IsEnabled(DynamoDbLogLevel.Debug);

        // Assert
        result.Should().BeTrue();
        mockLogger.Received(1).IsEnabled(MelLogLevel.Debug);
    }

    [Fact]
    public void IsEnabled_DelegatesToUnderlyingLogger_WhenDisabled()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Debug).Returns(false);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        var result = adapter.IsEnabled(DynamoDbLogLevel.Debug);

        // Assert
        result.Should().BeFalse();
        mockLogger.Received(1).IsEnabled(MelLogLevel.Debug);
    }

    #endregion

    #region Log Method Tests (Subtask 10.2)

    [Fact]
    public void LogTrace_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Trace).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogTrace(1000, "Test message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogDebug_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Debug).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogDebug(1001, "Test message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogInformation_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Information).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogInformation(3000, "Test message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogWarning_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Warning).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogWarning(2000, "Test message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogError_WithoutException_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogError(9000, "Test error message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogError_WithException_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Test exception");

        // Act
        adapter.LogError(9001, exception, "Test error message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCritical_CallsUnderlyingLoggerWithCorrectLevel()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Critical).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Test critical exception");

        // Act
        adapter.LogCritical(9999, exception, "Test critical message with {Param}", "value");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogMethods_WhenLogLevelDisabled_DoNotCallUnderlyingLogger()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(Arg.Any<MelLogLevel>()).Returns(false);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogTrace(1000, "Test");
        adapter.LogDebug(1001, "Test");
        adapter.LogInformation(3000, "Test");
        adapter.LogWarning(2000, "Test");
        adapter.LogError(9000, "Test");
        adapter.LogError(9001, new Exception(), "Test");
        adapter.LogCritical(9999, new Exception(), "Test");

        // Assert
        mockLogger.DidNotReceive().Log(
            Arg.Any<MelLogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Event ID Preservation Tests (Subtask 10.3)

    [Theory]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(3000)]
    [InlineData(9000)]
    public void LogTrace_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Trace).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogTrace(eventId, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Trace,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2001)]
    [InlineData(3001)]
    [InlineData(9001)]
    public void LogDebug_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Debug).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogDebug(eventId, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Debug,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(3000)]
    [InlineData(3100)]
    [InlineData(3110)]
    public void LogInformation_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Information).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogInformation(eventId, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Information,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2010)]
    [InlineData(2020)]
    public void LogWarning_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Warning).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogWarning(eventId, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Warning,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(9000)]
    [InlineData(9010)]
    [InlineData(9020)]
    public void LogError_WithoutException_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogError(eventId, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(9000)]
    [InlineData(9010)]
    [InlineData(9020)]
    public void LogError_WithException_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Test");

        // Act
        adapter.LogError(eventId, exception, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(9999)]
    [InlineData(9998)]
    [InlineData(9997)]
    public void LogCritical_PreservesEventId(int eventId)
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Critical).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Test");

        // Act
        adapter.LogCritical(eventId, exception, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Critical,
            Arg.Is<EventId>(e => e.Id == eventId),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Exception Logging Tests (Subtask 10.4)

    [Fact]
    public void LogError_WithException_PassesExceptionToUnderlyingLogger()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Test exception message");

        // Act
        adapter.LogError(9000, exception, "Error occurred");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex == exception),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCritical_WithException_PassesExceptionToUnderlyingLogger()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Critical).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var exception = new InvalidOperationException("Critical exception message");

        // Act
        adapter.LogCritical(9999, exception, "Critical error occurred");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex == exception),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogError_WithException_PreservesExceptionContext()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var innerException = new ArgumentException("Inner exception");
        var exception = new InvalidOperationException("Outer exception", innerException);

        // Act
        adapter.LogError(9000, exception, "Error with context");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => 
                ex == exception && 
                ex.InnerException == innerException &&
                ex.Message == "Outer exception"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCritical_WithException_PreservesExceptionContext()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Critical).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);
        var innerException = new ArgumentException("Inner exception");
        var exception = new InvalidOperationException("Outer exception", innerException);

        // Act
        adapter.LogCritical(9999, exception, "Critical error with context");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => 
                ex == exception && 
                ex.InnerException == innerException &&
                ex.Message == "Outer exception"),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogError_WithNullException_PassesNullToUnderlyingLogger()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(MelLogLevel.Error).Returns(true);
        var adapter = new MicrosoftExtensionsLoggingAdapter(mockLogger);

        // Act
        adapter.LogError(9000, (Exception)null!, "Error without exception");

        // Assert
        mockLogger.Received(1).Log(
            MelLogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception?>(ex => ex == null),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}
