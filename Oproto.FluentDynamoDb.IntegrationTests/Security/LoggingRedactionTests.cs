using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for logging redaction of sensitive fields.
/// Validates that sensitive field values are replaced with [REDACTED] in log output.
/// </summary>
[Collection("DynamoDB Local")]
public class LoggingRedactionTests : IntegrationTestBase
{
    public LoggingRedactionTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void RedactSensitiveFields_WithSensitiveData_RedactsCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "user-123" },
            ["name"] = new AttributeValue { S = "John Doe" },
            ["ssn"] = new AttributeValue { S = "123-45-6789" },
            ["email"] = new AttributeValue { S = "john@example.com" },
            ["public_data"] = new AttributeValue { S = "This is public" }
        };

        var sensitiveFields = new HashSet<string> { "ssn", "email" };

        // Act
        var redacted = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        redacted.Should().NotBeNull();
        redacted!["pk"].S.Should().Be("user-123", "non-sensitive field should not be redacted");
        redacted["name"].S.Should().Be("John Doe", "non-sensitive field should not be redacted");
        redacted["public_data"].S.Should().Be("This is public", "non-sensitive field should not be redacted");
        
        redacted["ssn"].S.Should().Be("[REDACTED]", "sensitive field should be redacted");
        redacted["email"].S.Should().Be("[REDACTED]", "sensitive field should be redacted");
    }

    [Fact]
    public void RedactSensitiveFields_WithNoSensitiveFields_ReturnsOriginal()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "user-123" },
            ["name"] = new AttributeValue { S = "John Doe" }
        };

        var sensitiveFields = new HashSet<string>();

        // Act
        var redacted = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        redacted.Should().BeSameAs(item, "should return original when no sensitive fields");
    }


    [Fact]
    public void RedactSensitiveFields_WithNullItem_ReturnsNull()
    {
        // Arrange
        Dictionary<string, AttributeValue>? item = null;
        var sensitiveFields = new HashSet<string> { "ssn" };

        // Act
        var redacted = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        redacted.Should().BeNull();
    }

    [Fact]
    public void RedactSensitiveFields_WithEmptyItem_ReturnsEmpty()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>();
        var sensitiveFields = new HashSet<string> { "ssn" };

        // Act
        var redacted = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        redacted.Should().BeSameAs(item);
        redacted.Should().BeEmpty();
    }

    [Fact]
    public void RedactIfSensitive_WithSensitiveField_ReturnsRedacted()
    {
        // Arrange
        var value = "123-45-6789";
        var fieldName = "ssn";
        var sensitiveFields = new HashSet<string> { "ssn", "email" };

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("[REDACTED]");
    }

    [Fact]
    public void RedactIfSensitive_WithNonSensitiveField_ReturnsOriginal()
    {
        // Arrange
        var value = "John Doe";
        var fieldName = "name";
        var sensitiveFields = new HashSet<string> { "ssn", "email" };

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void RedactIfSensitive_WithNullSensitiveFields_ReturnsOriginal()
    {
        // Arrange
        var value = "123-45-6789";
        var fieldName = "ssn";
        HashSet<string>? sensitiveFields = null;

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("123-45-6789");
    }
}
