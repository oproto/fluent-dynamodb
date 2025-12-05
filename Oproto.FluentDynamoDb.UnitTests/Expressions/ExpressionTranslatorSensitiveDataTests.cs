using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for sensitive data redaction in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorSensitiveDataTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Ssn { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Ssn",
                    AttributeName = "ssn",
                    PropertyType = typeof(string),
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Age",
                    AttributeName = "age",
                    PropertyType = typeof(int),
                    SupportedOperations = null
                }
            }
        };
    }

    [Fact]
    public void Translate_WithSensitiveProperty_RedactsValueInLogs()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        // Simulate SecurityMetadata.IsSensitiveField
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test@example.com");
        
        // Verify logging was called with redacted value
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            Arg.Any<string>(), // parameter name
            "[REDACTED]", // redacted value
            "Email"); // property name
    }

    [Fact]
    public void Translate_WithNonSensitiveProperty_DoesNotRedactValueInLogs()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        // Simulate SecurityMetadata.IsSensitiveField
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John Doe";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John Doe");
        
        // Verify logging was called with actual value
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            Arg.Any<string>(), // parameter name
            "John Doe", // actual value
            "Name"); // property name
    }

    [Fact]
    public void Translate_WithMultipleSensitiveProperties_RedactsAllSensitiveValues()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        // Simulate SecurityMetadata.IsSensitiveField
        bool IsSensitiveField(string attributeName) => attributeName == "email" || attributeName == "ssn";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com" && x.Ssn == "123-45-6789";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 = :p1)");
        
        // Verify both sensitive values were redacted in logs
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            ":p0",
            "[REDACTED]",
            "Email");
            
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            ":p1",
            "[REDACTED]",
            "Ssn");
    }

    [Fact]
    public void Translate_WithMixedSensitiveAndNonSensitive_RedactsOnlySensitiveValues()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        // Simulate SecurityMetadata.IsSensitiveField
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com" && x.Name == "John Doe";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 = :p1)");
        
        // Verify sensitive value was redacted
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            ":p0",
            "[REDACTED]",
            "Email");
            
        // Verify non-sensitive value was not redacted
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            ":p1",
            "John Doe",
            "Name");
    }

    [Fact]
    public void Translate_WithoutLogger_DoesNotThrow()
    {
        // Arrange
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator((Oproto.FluentDynamoDb.Logging.IDynamoDbLogger?)null, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Translate_WithLoggerDisabled_DoesNotLog()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(false);
        
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        
        // Verify no logging occurred
        logger.DidNotReceive().LogDebug(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }

    [Fact]
    public void Translate_WithoutSecurityMetadata_LogsWithoutRedaction()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        var translator = new ExpressionTranslator(logger, null);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        
        // Verify logging was called with actual value (no redaction)
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            Arg.Any<string>(),
            "test@example.com",
            "Email");
    }

    [Fact]
    public void Translate_PreservesPropertyNameInLogs()
    {
        // Arrange
        var logger = Substitute.For<IDynamoDbLogger>();
        logger.IsEnabled(LogLevel.Debug).Returns(true);
        
        bool IsSensitiveField(string attributeName) => attributeName == "email";
        
        var translator = new ExpressionTranslator(logger, IsSensitiveField);
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Email == "test@example.com";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Verify property name is preserved even though value is redacted
        logger.Received(1).LogDebug(
            LogEventIds.ExpressionTranslation,
            Arg.Is<string>(s => s.Contains("Expression parameter")),
            Arg.Any<string>(),
            "[REDACTED]",
            "Email"); // Property name should be present
    }
}
