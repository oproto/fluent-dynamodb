using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Logging.Extensions;

namespace Oproto.FluentDynamoDb.Logging.Extensions.UnitTests;

public class LoggerExtensionsTests
{
    #region ToDynamoDbLogger(ILogger) Tests (Subtask 10.5)

    [Fact]
    public void ToDynamoDbLogger_WithILogger_ReturnsAdapter()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();

        // Act
        var result = mockLogger.ToDynamoDbLogger();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MicrosoftExtensionsLoggingAdapter>();
    }

    [Fact]
    public void ToDynamoDbLogger_WithILogger_ThrowsWhenLoggerIsNull()
    {
        // Arrange
        ILogger nullLogger = null!;

        // Act
        var act = () => nullLogger.ToDynamoDbLogger();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void ToDynamoDbLogger_WithILogger_CreatesWorkingAdapter()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information).Returns(true);

        // Act
        var adapter = mockLogger.ToDynamoDbLogger();
        adapter.LogInformation(3000, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region ToDynamoDbLogger(ILoggerFactory, string) Tests (Subtask 10.5)

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryAndCategoryName_ReturnsAdapter()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();
        var mockLogger = Substitute.For<ILogger>();
        mockLoggerFactory.CreateLogger("TestCategory").Returns(mockLogger);

        // Act
        var result = mockLoggerFactory.ToDynamoDbLogger("TestCategory");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MicrosoftExtensionsLoggingAdapter>();
        mockLoggerFactory.Received(1).CreateLogger("TestCategory");
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryAndCategoryName_ThrowsWhenFactoryIsNull()
    {
        // Arrange
        ILoggerFactory nullFactory = null!;

        // Act
        var act = () => nullFactory.ToDynamoDbLogger("TestCategory");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryAndCategoryName_ThrowsWhenCategoryNameIsNull()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();

        // Act
        var act = () => mockLoggerFactory.ToDynamoDbLogger((string)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("categoryName");
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryAndCategoryName_CreatesWorkingAdapter()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();
        var mockLogger = Substitute.For<ILogger>();
        mockLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug).Returns(true);
        mockLoggerFactory.CreateLogger("TestCategory").Returns(mockLogger);

        // Act
        var adapter = mockLoggerFactory.ToDynamoDbLogger("TestCategory");
        adapter.LogDebug(1001, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            Microsoft.Extensions.Logging.LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region ToDynamoDbLogger<T>(ILoggerFactory) Tests (Subtask 10.5)

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryGeneric_ReturnsAdapter()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();
        var mockLogger = Substitute.For<ILogger<LoggerExtensionsTests>>();
        mockLoggerFactory.CreateLogger<LoggerExtensionsTests>().Returns(mockLogger);

        // Act
        var result = mockLoggerFactory.ToDynamoDbLogger<LoggerExtensionsTests>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<MicrosoftExtensionsLoggingAdapter>();
        mockLoggerFactory.Received(1).CreateLogger<LoggerExtensionsTests>();
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryGeneric_ThrowsWhenFactoryIsNull()
    {
        // Arrange
        ILoggerFactory nullFactory = null!;

        // Act
        var act = () => nullFactory.ToDynamoDbLogger<LoggerExtensionsTests>();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryGeneric_CreatesWorkingAdapter()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();
        var mockLogger = Substitute.For<ILogger<LoggerExtensionsTests>>();
        mockLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace).Returns(true);
        mockLoggerFactory.CreateLogger<LoggerExtensionsTests>().Returns(mockLogger);

        // Act
        var adapter = mockLoggerFactory.ToDynamoDbLogger<LoggerExtensionsTests>();
        adapter.LogTrace(1000, "Test message");

        // Assert
        mockLogger.Received(1).Log(
            Microsoft.Extensions.Logging.LogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ToDynamoDbLogger_WithILoggerFactoryGeneric_UsesDifferentTypesCorrectly()
    {
        // Arrange
        var mockLoggerFactory = Substitute.For<ILoggerFactory>();
        var mockLogger1 = Substitute.For<ILogger<LoggerExtensionsTests>>();
        var mockLogger2 = Substitute.For<ILogger<MicrosoftExtensionsLoggingAdapterTests>>();
        mockLoggerFactory.CreateLogger<LoggerExtensionsTests>().Returns(mockLogger1);
        mockLoggerFactory.CreateLogger<MicrosoftExtensionsLoggingAdapterTests>().Returns(mockLogger2);

        // Act
        var adapter1 = mockLoggerFactory.ToDynamoDbLogger<LoggerExtensionsTests>();
        var adapter2 = mockLoggerFactory.ToDynamoDbLogger<MicrosoftExtensionsLoggingAdapterTests>();

        // Assert
        adapter1.Should().NotBeNull();
        adapter2.Should().NotBeNull();
        adapter1.Should().NotBeSameAs(adapter2);
        mockLoggerFactory.Received(1).CreateLogger<LoggerExtensionsTests>();
        mockLoggerFactory.Received(1).CreateLogger<MicrosoftExtensionsLoggingAdapterTests>();
    }

    #endregion
}
