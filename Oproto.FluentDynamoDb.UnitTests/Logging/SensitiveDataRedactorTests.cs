using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.UnitTests.Logging;

public class SensitiveDataRedactorTests
{
    private const string RedactedPlaceholder = "[REDACTED]";

    #region RedactSensitiveFields Tests

    [Fact]
    public void RedactSensitiveFields_WithSingleSensitiveField_RedactsValueAndPreservesFieldName()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" },
            ["Name"] = new AttributeValue { S = "John Doe" }
        };
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("Email");
        result!["Email"].S.Should().Be(RedactedPlaceholder);
        result["UserId"].S.Should().Be("user-123");
        result["Name"].S.Should().Be("John Doe");
    }

    [Fact]
    public void RedactSensitiveFields_WithMultipleSensitiveFields_RedactsAllSensitiveValues()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" },
            ["PhoneNumber"] = new AttributeValue { S = "+1234567890" },
            ["Name"] = new AttributeValue { S = "John Doe" },
            ["SocialSecurityNumber"] = new AttributeValue { S = "123-45-6789" }
        };
        var sensitiveFields = new HashSet<string> { "Email", "PhoneNumber", "SocialSecurityNumber" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result!["Email"].S.Should().Be(RedactedPlaceholder);
        result["PhoneNumber"].S.Should().Be(RedactedPlaceholder);
        result["SocialSecurityNumber"].S.Should().Be(RedactedPlaceholder);
        result["UserId"].S.Should().Be("user-123");
        result["Name"].S.Should().Be("John Doe");
    }

    [Fact]
    public void RedactSensitiveFields_WithMultipleSensitiveFields_PreservesNonSensitiveFields()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" },
            ["Name"] = new AttributeValue { S = "John Doe" },
            ["Age"] = new AttributeValue { N = "30" },
            ["IsActive"] = new AttributeValue { BOOL = true }
        };
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result!["UserId"].S.Should().Be("user-123");
        result["Name"].S.Should().Be("John Doe");
        result["Age"].N.Should().Be("30");
        result["IsActive"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void RedactSensitiveFields_WithNullItem_ReturnsNull()
    {
        // Arrange
        Dictionary<string, AttributeValue>? item = null;
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RedactSensitiveFields_WithEmptyItem_ReturnsEmptyItem()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>();
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        result.Should().BeSameAs(item);
    }

    [Fact]
    public void RedactSensitiveFields_WithNullSensitiveFields_ReturnsOriginalItem()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" }
        };
        HashSet<string>? sensitiveFields = null;

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(item);
        result!["Email"].S.Should().Be("user@example.com");
    }

    [Fact]
    public void RedactSensitiveFields_WithEmptySensitiveFields_ReturnsOriginalItem()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" }
        };
        var sensitiveFields = new HashSet<string>();

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(item);
        result!["Email"].S.Should().Be("user@example.com");
    }

    [Fact]
    public void RedactSensitiveFields_WithNonExistentSensitiveField_DoesNotAffectItem()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Name"] = new AttributeValue { S = "John Doe" }
        };
        var sensitiveFields = new HashSet<string> { "Email", "PhoneNumber" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeNull();
        result!["UserId"].S.Should().Be("user-123");
        result["Name"].S.Should().Be("John Doe");
        result.Should().NotContainKey("Email");
        result.Should().NotContainKey("PhoneNumber");
    }

    [Fact]
    public void RedactSensitiveFields_CreatesNewDictionary_DoesNotModifyOriginal()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new AttributeValue { S = "user-123" },
            ["Email"] = new AttributeValue { S = "user@example.com" }
        };
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactSensitiveFields(item, sensitiveFields);

        // Assert
        result.Should().NotBeSameAs(item);
        item["Email"].S.Should().Be("user@example.com"); // Original unchanged
        result!["Email"].S.Should().Be(RedactedPlaceholder); // Result redacted
    }

    #endregion

    #region RedactIfSensitive Tests

    [Fact]
    public void RedactIfSensitive_WithSensitiveField_ReturnsRedactedPlaceholder()
    {
        // Arrange
        var value = "sensitive-value";
        var fieldName = "Email";
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be(RedactedPlaceholder);
    }

    [Fact]
    public void RedactIfSensitive_WithNonSensitiveField_ReturnsOriginalValue()
    {
        // Arrange
        var value = "non-sensitive-value";
        var fieldName = "Name";
        var sensitiveFields = new HashSet<string> { "Email" };

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("non-sensitive-value");
    }

    [Fact]
    public void RedactIfSensitive_WithNullSensitiveFields_ReturnsOriginalValue()
    {
        // Arrange
        var value = "some-value";
        var fieldName = "Email";
        HashSet<string>? sensitiveFields = null;

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("some-value");
    }

    [Fact]
    public void RedactIfSensitive_WithEmptySensitiveFields_ReturnsOriginalValue()
    {
        // Arrange
        var value = "some-value";
        var fieldName = "Email";
        var sensitiveFields = new HashSet<string>();

        // Act
        var result = SensitiveDataRedactor.RedactIfSensitive(value, fieldName, sensitiveFields);

        // Assert
        result.Should().Be("some-value");
    }

    #endregion
}
