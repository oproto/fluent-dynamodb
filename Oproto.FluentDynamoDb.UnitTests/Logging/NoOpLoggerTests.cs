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

/// <summary>
/// Tests that verify the IsEnabled guard pattern works correctly with NoOpLogger.
/// This pattern is used throughout the library to skip logging when disabled.
/// </summary>
public class NoOpLoggerIsEnabledGuardTests
{
    /// <summary>
    /// Verifies that when using the IsEnabled guard pattern with NoOpLogger,
    /// the logging code block is never executed.
    /// **Property 1 (partial): Logging disabled behavior**
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Fact]
    public void IsEnabledGuardPattern_WithNoOpLogger_SkipsLoggingCode()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        var loggingCodeExecuted = false;
        
        // Act - Simulate the guard pattern used throughout the library
        if (logger.IsEnabled(LogLevel.Information))
        {
            loggingCodeExecuted = true;
            logger.LogInformation(1000, "This should never be called");
        }
        
        // Assert - The logging code should never execute
        loggingCodeExecuted.Should().BeFalse("NoOpLogger.IsEnabled() should return false, preventing logging code execution");
    }
    
    /// <summary>
    /// Verifies that the IsEnabled guard pattern prevents expensive parameter evaluation.
    /// This is critical for performance when logging is disabled.
    /// **Property 1 (partial): Logging disabled behavior**
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Fact]
    public void IsEnabledGuardPattern_WithNoOpLogger_PreventsExpensiveParameterEvaluation()
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        var expensiveOperationCalled = false;
        
        string ExpensiveOperation()
        {
            expensiveOperationCalled = true;
            return "expensive result";
        }
        
        // Act - Simulate the guard pattern with expensive parameter
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(1001, "Result: {Result}", ExpensiveOperation());
        }
        
        // Assert - The expensive operation should never be called
        expensiveOperationCalled.Should().BeFalse("IsEnabled guard should prevent expensive parameter evaluation");
    }
    
    /// <summary>
    /// Verifies that all log levels are properly guarded by IsEnabled.
    /// **Property 1 (partial): Logging disabled behavior**
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void IsEnabledGuardPattern_WithNoOpLogger_SkipsAllLogLevels(LogLevel logLevel)
    {
        // Arrange
        var logger = NoOpLogger.Instance;
        var loggingCodeExecuted = false;
        
        // Act
        if (logger.IsEnabled(logLevel))
        {
            loggingCodeExecuted = true;
        }
        
        // Assert
        loggingCodeExecuted.Should().BeFalse($"NoOpLogger.IsEnabled({logLevel}) should return false");
    }
    
    /// <summary>
    /// Verifies that FluentDynamoDbOptions with NoOpLogger properly skips logging.
    /// This tests the integration with the options pattern used throughout the library.
    /// **Property 1 (partial): Logging disabled behavior**
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Fact]
    public void FluentDynamoDbOptions_WithNoOpLogger_SkipsLogging()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithLogger(NoOpLogger.Instance);
        var loggingCodeExecuted = false;
        
        // Act - Simulate the pattern used in generated code
        if (options.Logger?.IsEnabled(LogLevel.Trace) == true)
        {
            loggingCodeExecuted = true;
            options.Logger.LogTrace(1000, "This should never be called");
        }
        
        // Assert
        loggingCodeExecuted.Should().BeFalse("FluentDynamoDbOptions with NoOpLogger should skip logging");
    }
}
