using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.UnitTests.Validation;

/// <summary>
/// Unit tests for schema validation logging integration.
/// Verifies correct log levels and messages for schema validation results.
/// </summary>
public class SchemaValidationLoggingTests
{
    [Fact]
    public void SchemaValidationEventIds_AreInCorrectRange()
    {
        // Arrange
        var schemaValidationEventIds = new[]
        {
            LogEventIds.SchemaValidationStart,
            LogEventIds.SchemaValidationSuccess,
            LogEventIds.SchemaValidationError,
            LogEventIds.SchemaValidationWarning
        };
        
        // Act & Assert
        foreach (var eventId in schemaValidationEventIds)
        {
            eventId.Should().BeInRange(5000, 5099, 
                $"schema validation event IDs should be in range 5000-5099, but {eventId} is not");
        }
    }
    
    [Fact]
    public void SchemaValidationStart_HasCorrectValue()
    {
        LogEventIds.SchemaValidationStart.Should().Be(5000);
    }
    
    [Fact]
    public void SchemaValidationSuccess_HasCorrectValue()
    {
        LogEventIds.SchemaValidationSuccess.Should().Be(5001);
    }
    
    [Fact]
    public void SchemaValidationError_HasCorrectValue()
    {
        LogEventIds.SchemaValidationError.Should().Be(5010);
    }
    
    [Fact]
    public void SchemaValidationWarning_HasCorrectValue()
    {
        LogEventIds.SchemaValidationWarning.Should().Be(5011);
    }


    [Fact]
    public void LogResults_WithErrors_LogsAtErrorLevel()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var error = new SchemaValidationError(
            SchemaValidationErrorCode.PartitionKeyNameMismatch,
            "pk",
            "expected_pk",
            "actual_pk",
            "Partition key name mismatch");
        
        var result = new SchemaValidationResult(
            new[] { error },
            Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.Received(1).LogError(
            LogEventIds.SchemaValidationError,
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void LogResults_WithWarnings_LogsAtWarningLevel()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var warning = new SchemaValidationWarning(
            SchemaValidationWarningCode.UnexpectedGsi,
            "extra_gsi",
            "Unexpected GSI found in table");
        
        var result = new SchemaValidationResult(
            Array.Empty<SchemaValidationError>(),
            new[] { warning });
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.Received(1).LogWarning(
            LogEventIds.SchemaValidationWarning,
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void LogResults_WithMultipleErrors_LogsEachError()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var errors = new[]
        {
            new SchemaValidationError(
                SchemaValidationErrorCode.PartitionKeyNameMismatch,
                "pk",
                "expected_pk",
                "actual_pk",
                "Partition key name mismatch"),
            new SchemaValidationError(
                SchemaValidationErrorCode.SortKeyMissing,
                "sk",
                "expected_sk",
                "none",
                "Sort key missing"),
            new SchemaValidationError(
                SchemaValidationErrorCode.GsiNotFound,
                "gsi_name",
                "gsi_name",
                "not found",
                "GSI not found")
        };
        
        var result = new SchemaValidationResult(errors, Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.Received(3).LogError(
            LogEventIds.SchemaValidationError,
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void LogResults_WithMultipleWarnings_LogsEachWarning()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var warnings = new[]
        {
            new SchemaValidationWarning(
                SchemaValidationWarningCode.UnexpectedGsi,
                "extra_gsi_1",
                "Unexpected GSI found"),
            new SchemaValidationWarning(
                SchemaValidationWarningCode.UnexpectedLsi,
                "extra_lsi_1",
                "Unexpected LSI found")
        };
        
        var result = new SchemaValidationResult(Array.Empty<SchemaValidationError>(), warnings);
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.Received(2).LogWarning(
            LogEventIds.SchemaValidationWarning,
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }


    [Fact]
    public void LogResults_WithErrorsAndWarnings_LogsBothTypes()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var error = new SchemaValidationError(
            SchemaValidationErrorCode.TtlNotEnabled,
            "ttl",
            "enabled",
            "disabled",
            "TTL not enabled");
        var warning = new SchemaValidationWarning(
            SchemaValidationWarningCode.UnexpectedTtl,
            "ttl_attr",
            "Unexpected TTL configuration");
        
        var result = new SchemaValidationResult(
            new[] { error },
            new[] { warning });
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.Received(1).LogError(
            LogEventIds.SchemaValidationError,
            Arg.Any<string>(),
            Arg.Any<object[]>());
        logger.Received(1).LogWarning(
            LogEventIds.SchemaValidationWarning,
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void LogResults_WithNoErrorsOrWarnings_DoesNotLog()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        var result = new SchemaValidationResult(
            Array.Empty<SchemaValidationError>(),
            Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        logger.DidNotReceive().LogError(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>());
        logger.DidNotReceive().LogWarning(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void LogResults_ErrorMessage_ContainsErrorCode()
    {
        // Arrange
        var capturedArgs = new List<object[]>();
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.When(x => x.LogError(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>()))
            .Do(callInfo => capturedArgs.Add((object[])callInfo[2]));
        
        var error = new SchemaValidationError(
            SchemaValidationErrorCode.GsiPartitionKeyNameMismatch,
            "gsi_pk",
            "expected_name",
            "actual_name",
            "GSI partition key name mismatch");
        
        var result = new SchemaValidationResult(
            new[] { error },
            Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().HaveCountGreaterThan(0);
        capturedArgs[0][0].Should().Be(SchemaValidationErrorCode.GsiPartitionKeyNameMismatch);
    }
    
    [Fact]
    public void LogResults_ErrorMessage_ContainsElement()
    {
        // Arrange
        var capturedArgs = new List<object[]>();
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.When(x => x.LogError(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>()))
            .Do(callInfo => capturedArgs.Add((object[])callInfo[2]));
        
        const string elementName = "test_element";
        var error = new SchemaValidationError(
            SchemaValidationErrorCode.LsiNotFound,
            elementName,
            "expected",
            "actual",
            "LSI not found");
        
        var result = new SchemaValidationResult(
            new[] { error },
            Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().HaveCountGreaterThan(1);
        capturedArgs[0][1].Should().Be(elementName);
    }


    [Fact]
    public void LogResults_ErrorMessage_ContainsExpectedAndActual()
    {
        // Arrange
        var capturedArgs = new List<object[]>();
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.When(x => x.LogError(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>()))
            .Do(callInfo => capturedArgs.Add((object[])callInfo[2]));
        
        const string expectedValue = "expected_value";
        const string actualValue = "actual_value";
        var error = new SchemaValidationError(
            SchemaValidationErrorCode.PartitionKeyTypeMismatch,
            "pk_type",
            expectedValue,
            actualValue,
            "Partition key type mismatch");
        
        var result = new SchemaValidationResult(
            new[] { error },
            Array.Empty<SchemaValidationWarning>());
        
        // Act
        result.LogResults(logger);
        
        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().HaveCountGreaterThanOrEqualTo(5);
        capturedArgs[0][3].Should().Be(expectedValue);
        capturedArgs[0][4].Should().Be(actualValue);
    }
    
    [Fact]
    public void LogResults_WarningMessage_ContainsWarningCode()
    {
        // Arrange
        var capturedArgs = new List<object[]>();
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.When(x => x.LogWarning(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>()))
            .Do(callInfo => capturedArgs.Add((object[])callInfo[2]));
        
        var warning = new SchemaValidationWarning(
            SchemaValidationWarningCode.ProjectionModelRecommended,
            "index_name",
            "Projection model recommended for non-ALL projection");
        
        var result = new SchemaValidationResult(
            Array.Empty<SchemaValidationError>(),
            new[] { warning });
        
        // Act
        result.LogResults(logger);
        
        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().HaveCountGreaterThan(0);
        capturedArgs[0][0].Should().Be(SchemaValidationWarningCode.ProjectionModelRecommended);
    }
    
    [Fact]
    public void LogResults_WarningMessage_ContainsElement()
    {
        // Arrange
        var capturedArgs = new List<object[]>();
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.When(x => x.LogWarning(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>()))
            .Do(callInfo => capturedArgs.Add((object[])callInfo[2]));
        
        const string elementName = "warning_element";
        var warning = new SchemaValidationWarning(
            SchemaValidationWarningCode.UnexpectedLsi,
            elementName,
            "Unexpected LSI found");
        
        var result = new SchemaValidationResult(
            Array.Empty<SchemaValidationError>(),
            new[] { warning });
        
        // Act
        result.LogResults(logger);
        
        // Assert
        capturedArgs.Should().HaveCount(1);
        capturedArgs[0].Should().HaveCountGreaterThan(1);
        capturedArgs[0][1].Should().Be(elementName);
    }
}
