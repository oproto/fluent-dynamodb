using AwesomeAssertions;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.UnitTests.Logging;

public class IDynamoDbLoggerTests
{
    [Fact]
    public void CustomImplementation_CanImplementInterface()
    {
        // Arrange & Act
        var logger = new TestLogger();
        
        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeAssignableTo<IDynamoDbLogger>();
    }
    
    [Fact]
    public void IsEnabled_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var result = logger.IsEnabled(LogLevel.Information);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void LogTrace_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var act = () => logger.LogTrace(1000, "Test message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Test message");
    }
    
    [Fact]
    public void LogDebug_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var act = () => logger.LogDebug(1001, "Debug message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Debug message");
    }
    
    [Fact]
    public void LogInformation_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var act = () => logger.LogInformation(3000, "Info message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Info message");
    }
    
    [Fact]
    public void LogWarning_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var act = () => logger.LogWarning(2000, "Warning message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Warning message");
    }
    
    [Fact]
    public void LogError_WithoutException_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        
        // Act
        var act = () => logger.LogError(9000, "Error message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Error message");
    }
    
    [Fact]
    public void LogError_WithException_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        var act = () => logger.LogError(9001, exception, "Error message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Error message");
        logger.LoggedExceptions.Should().ContainSingle();
        logger.LoggedExceptions[0].Should().Be(exception);
    }
    
    [Fact]
    public void LogCritical_CanBeCalled()
    {
        // Arrange
        var logger = new TestLogger();
        var exception = new InvalidOperationException("Critical exception");
        
        // Act
        var act = () => logger.LogCritical(9999, exception, "Critical message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().ContainSingle();
        logger.LoggedMessages[0].Should().Contain("Critical message");
        logger.LoggedExceptions.Should().ContainSingle();
        logger.LoggedExceptions[0].Should().Be(exception);
    }
    
    [Fact]
    public void AllMethods_CanBeCalledInSequence()
    {
        // Arrange
        var logger = new TestLogger();
        var exception = new InvalidOperationException("Test");
        
        // Act
        var act = () =>
        {
            logger.LogTrace(1, "Trace");
            logger.LogDebug(2, "Debug");
            logger.LogInformation(3, "Info");
            logger.LogWarning(4, "Warning");
            logger.LogError(5, "Error");
            logger.LogError(6, exception, "Error with exception");
            logger.LogCritical(7, exception, "Critical");
        };
        
        // Assert
        act.Should().NotThrow();
        logger.LoggedMessages.Should().HaveCount(7);
    }
    
    // Test implementation of IDynamoDbLogger for testing purposes
    private class TestLogger : IDynamoDbLogger
    {
        public List<string> LoggedMessages { get; } = new();
        public List<Exception> LoggedExceptions { get; } = new();
        
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void LogTrace(int eventId, string message, params object[] args)
        {
            LoggedMessages.Add(message);
        }
        
        public void LogDebug(int eventId, string message, params object[] args)
        {
            LoggedMessages.Add(message);
        }
        
        public void LogInformation(int eventId, string message, params object[] args)
        {
            LoggedMessages.Add(message);
        }
        
        public void LogWarning(int eventId, string message, params object[] args)
        {
            LoggedMessages.Add(message);
        }
        
        public void LogError(int eventId, string message, params object[] args)
        {
            LoggedMessages.Add(message);
        }
        
        public void LogError(int eventId, Exception exception, string message, params object[] args)
        {
            LoggedMessages.Add(message);
            LoggedExceptions.Add(exception);
        }
        
        public void LogCritical(int eventId, Exception exception, string message, params object[] args)
        {
            LoggedMessages.Add(message);
            LoggedExceptions.Add(exception);
        }
    }
}
