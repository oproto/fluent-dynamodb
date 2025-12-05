using AwesomeAssertions;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.UnitTests.Logging;

public class NoOpLoggerTests
{
    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Arrange & Act
        var instance1 = NoOpLogger.Instance;
        var instance2 = NoOpLogger.Instance;
        
        // Assert
        instance1.Should().NotBeNull();
        instance2.Should().NotBeNull();
        instance1.Should().BeSameAs(instance2);
    }
    
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public void IsEnabled_AlwaysReturnsFalse(LogLevel logLevel)
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var result = logger.IsEnabled(logLevel);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void LogTrace_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var act = () => logger.LogTrace(1000, "Test message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogDebug_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var act = () => logger.LogDebug(1001, "Test message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogInformation_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var act = () => logger.LogInformation(3000, "Test message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogWarning_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var act = () => logger.LogWarning(2000, "Test message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogError_WithoutException_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act
        var act = () => logger.LogError(9000, "Test error message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogError_WithException_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        var act = () => logger.LogError(9001, exception, "Test error message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void LogCritical_DoesNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        var exception = new InvalidOperationException("Test critical exception");
        
        // Act
        var act = () => logger.LogCritical(9999, exception, "Test critical message with {Param}", "value");
        
        // Assert
        act.Should().NotThrow();
    }
    
    [Fact]
    public void AllLogMethods_WithNullArguments_DoNotThrow()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        
        // Act & Assert
        var act = () =>
        {
            logger.LogTrace(1, null!);
            logger.LogDebug(2, null!);
            logger.LogInformation(3, null!);
            logger.LogWarning(4, null!);
            logger.LogError(5, (Exception)null!, null!);
            logger.LogCritical(7, null!, null!);
        };
        
        act.Should().NotThrow();
    }
}
