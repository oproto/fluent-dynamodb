using System.Globalization;
using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithConditionExpressionExtensionsTests
{
    private readonly TestBuilder _builder = new();

    #region Basic Where Method Tests

    [Fact]
    public void Where_SimpleExpression_ShouldSetConditionExpression()
    {
        // Act
        var result = _builder.Where("pk = :pk");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :pk");
    }

    [Fact]
    public void Where_ComplexExpression_ShouldSetConditionExpression()
    {
        // Act
        var result = _builder.Where("pk = :pk AND begins_with(sk, :prefix) AND #status = :status");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :pk AND begins_with(sk, :prefix) AND #status = :status");
    }

    [Fact]
    public void Where_EmptyExpression_ShouldSetEmptyConditionExpression()
    {
        // Act
        var result = _builder.Where("");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("");
    }

    #endregion

    #region Format String Tests - Basic Functionality

    [Fact]
    public void Where_FormatString_SingleParameter_ShouldReplaceWithGeneratedParameter()
    {
        // Act
        var result = _builder.Where("pk = {0}", "USER#123");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("USER#123");
    }

    [Fact]
    public void Where_FormatString_MultipleParameters_ShouldReplaceAllWithGeneratedParameters()
    {
        // Act
        var result = _builder.Where("pk = {0} AND sk = {1}", "USER#123", "ORDER#456");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :p0 AND sk = :p1");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("USER#123");
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("ORDER#456");
    }

    [Fact]
    public void Where_FormatString_RepeatedParameterIndex_ShouldReuseParameter()
    {
        // Act
        var result = _builder.Where("pk = {0} AND sk BETWEEN {1} AND {0}", "USER#123", "A");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :p1 AND :p2");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(3);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("USER#123");
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("A");
        _builder.AttributeValueHelper.AttributeValues[":p2"].S.Should().Be("USER#123");
    }

    [Fact]
    public void Where_FormatString_NoPlaceholders_ShouldReturnOriginalExpression()
    {
        // Act
        var result = _builder.Where("pk = :pk AND sk = :sk", "unused1", "unused2");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :pk AND sk = :sk");
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region Format String Tests - Data Types

    [Fact]
    public void Where_FormatString_StringValue_ShouldCreateStringAttributeValue()
    {
        // Act
        var result = _builder.Where("name = {0}", "John Doe");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("John Doe");
    }

    [Fact]
    public void Where_FormatString_BooleanValue_ShouldCreateBooleanAttributeValue()
    {
        // Act
        var result = _builder.Where("active = {0}", true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].BOOL.Should().BeTrue();
        _builder.AttributeValueHelper.AttributeValues[":p0"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void Where_FormatString_IntegerValue_ShouldCreateNumericAttributeValue()
    {
        // Act
        var result = _builder.Where("count = {0}", 42);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("42");
    }

    [Fact]
    public void Where_FormatString_DecimalValue_ShouldCreateNumericAttributeValue()
    {
        // Act
        var result = _builder.Where("amount = {0}", 123.45m);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("123.45");
    }

    [Fact]
    public void Where_FormatString_DoubleValue_ShouldCreateNumericAttributeValue()
    {
        // Act
        var result = _builder.Where("price = {0}", 99.99);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("99.99");
    }

    [Fact]
    public void Where_FormatString_NullValue_ShouldCreateNullAttributeValue()
    {
        // Act
        var result = _builder.Where("optional = {0}", (string?)null);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Where_FormatString_EnumValue_ShouldCreateStringAttributeValue()
    {
        // Act
        var result = _builder.Where("status = {0}", TestEnum.Active);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("Active");
    }

    [Fact]
    public void Where_FormatString_GuidValue_ShouldCreateStringAttributeValue()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var result = _builder.Where("id = {0}", guid);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    #endregion

    #region Format String Tests - DateTime Formatting

    [Fact]
    public void Where_FormatString_DateTime_NoFormat_ShouldUseISOFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var result = _builder.Where("created = {0}", dateTime);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000Z");
    }

    [Fact]
    public void Where_FormatString_DateTime_ISOFormat_ShouldUseISOFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var result = _builder.Where("created = {0:o}", dateTime);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000Z");
    }

    [Fact]
    public void Where_FormatString_DateTime_CustomFormat_ShouldUseCustomFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45);

        // Act
        var result = _builder.Where("created = {0:yyyy-MM-dd}", dateTime);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("2024-01-15");
    }

    [Fact]
    public void Where_FormatString_DateTimeOffset_ShouldFormatCorrectly()
    {
        // Arrange
        var dateTimeOffset = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(-5));

        // Act
        var result = _builder.Where("created = {0:o}", dateTimeOffset);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000-05:00");
    }

    #endregion

    #region Format String Tests - Numeric Formatting

    [Fact]
    public void Where_FormatString_Decimal_F2Format_ShouldFormatToTwoDecimalPlaces()
    {
        // Act
        var result = _builder.Where("amount = {0:F2}", 123.456m);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("123.46");
    }

    [Fact]
    public void Where_FormatString_Integer_HexFormat_ShouldFormatAsHex()
    {
        // Act
        var result = _builder.Where("flags = {0:X}", 255);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("FF");
    }

    [Fact]
    public void Where_FormatString_Double_ExponentialFormat_ShouldFormatAsExponential()
    {
        // Act
        var result = _builder.Where("value = {0:E2}", 1234.5);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("1.23E+003");
    }

    #endregion

    #region Format String Tests - Guid Formatting

    [Fact]
    public void Where_FormatString_Guid_NFormat_ShouldFormatWithoutHyphens()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var result = _builder.Where("id = {0:N}", guid);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("12345678123412341234123456789012");
    }

    [Fact]
    public void Where_FormatString_Guid_BFormat_ShouldFormatWithBraces()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var result = _builder.Where("id = {0:B}", guid);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("{12345678-1234-1234-1234-123456789012}");
    }

    #endregion

    #region Format String Tests - Error Conditions

    [Fact]
    public void Where_FormatString_NullFormat_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Where(null as string, "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void Where_FormatString_EmptyFormat_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Where("", "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void Where_FormatString_NullArgs_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = {0}", null as object[]);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("args");
    }

    [Fact]
    public void Where_FormatString_ParameterIndexOutOfRange_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = {0} AND sk = {1}", "value1");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string references parameter index 1 but only 1 arguments were provided.*");
    }

    [Fact]
    public void Where_FormatString_NegativeParameterIndex_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = {-1}", "value");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains invalid parameter indices: -1.*");
    }

    [Fact]
    public void Where_FormatString_InvalidParameterIndex_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = {abc}", "value");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains invalid parameter indices: abc.*");
    }

    [Fact]
    public void Where_FormatString_UnmatchedOpenBrace_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = {0", "value");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains unmatched braces.*");
    }

    [Fact]
    public void Where_FormatString_UnmatchedCloseBrace_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("pk = 0}", "value");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains unmatched braces.*");
    }

    [Fact]
    public void Where_FormatString_InvalidFormatSpecifier_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("value = {0:INVALID}", 123);
        action.Should().Throw<FormatException>()
            .WithMessage("Invalid format specifier 'INVALID' for parameter at index 0.*");
    }

    [Fact]
    public void Where_FormatString_BooleanWithFormat_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("active = {0:F2}", true);
        action.Should().Throw<FormatException>()
            .WithMessage("Invalid format specifier 'F2' for parameter at index 0.*");
    }

    [Fact]
    public void Where_FormatString_EnumWithFormat_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.Where("status = {0:F2}", TestEnum.Active);
        action.Should().Throw<FormatException>()
            .WithMessage("Invalid format specifier 'F2' for parameter at index 0.*");
    }

    #endregion

    #region Format String Tests - Complex Scenarios

    [Fact]
    public void Where_FormatString_ComplexExpressionWithMixedTypes_ShouldProcessCorrectly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var result = _builder.Where(
            "pk = {0} AND created > {1:o} AND amount >= {2:F2} AND active = {3}",
            "USER#123", dateTime, 99.99m, true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("pk = :p0 AND created > :p1 AND amount >= :p2 AND active = :p3");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(4);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("USER#123");
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("2024-01-15T10:30:45.0000000Z");
        _builder.AttributeValueHelper.AttributeValues[":p2"].N.Should().Be("99.99");
        _builder.AttributeValueHelper.AttributeValues[":p3"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Where_FormatString_FunctionCallsWithParameters_ShouldProcessCorrectly()
    {
        // Act
        var result = _builder.Where(
            "begins_with(sk, {0}) AND contains(#name, {1}) AND size(#tags) > {2}",
            "ORDER#", "John", 5);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("begins_with(sk, :p0) AND contains(#name, :p1) AND size(#tags) > :p2");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(3);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("ORDER#");
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("John");
        _builder.AttributeValueHelper.AttributeValues[":p2"].N.Should().Be("5");
    }

    [Fact]
    public void Where_FormatString_NestedBracesInStrings_ShouldNotInterfereWithFormatting()
    {
        // Act
        var result = _builder.Where("data = {0}", "{\"key\": \"value\"}");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("data = :p0");
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public void Where_FormatString_MultipleCallsWithSameBuilder_ShouldIncrementParameterNames()
    {
        // Act
        _builder.Where("pk = {0}", "USER#123");
        var result = _builder.Where("sk = {0}", "ORDER#456");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.ConditionExpression.Should().Be("sk = :p1"); // Second call should use :p1
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("USER#123");
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("ORDER#456");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Where_FormatString_EmptyStringParameter_ShouldCreateEmptyStringAttributeValue()
    {
        // Act
        var result = _builder.Where("name = {0}", "");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("");
    }

    [Fact]
    public void Where_FormatString_WhitespaceStringParameter_ShouldPreserveWhitespace()
    {
        // Act
        var result = _builder.Where("name = {0}", "   ");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be("   ");
    }

    [Fact]
    public void Where_FormatString_ZeroValue_ShouldCreateZeroAttributeValue()
    {
        // Act
        var result = _builder.Where("count = {0}", 0);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].N.Should().Be("0");
    }

    [Fact]
    public void Where_FormatString_FalseValue_ShouldCreateFalseAttributeValue()
    {
        // Act
        var result = _builder.Where("active = {0}", false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues[":p0"].BOOL.Should().BeFalse();
        _builder.AttributeValueHelper.AttributeValues[":p0"].IsBOOLSet.Should().BeTrue();
    }

    #endregion

    // Test enum for testing enum formatting
    private enum TestEnum
    {
        Active,
        Inactive,
        Pending
    }

    // Test builder class for testing extension methods
    private class TestBuilder : IWithConditionExpression<TestBuilder>
    {
        public AttributeValueInternal AttributeValueHelper { get; } = new();
        public string ConditionExpression { get; private set; } = string.Empty;
        public TestBuilder Self => this;

        public AttributeValueInternal GetAttributeValueHelper() => AttributeValueHelper;

        public TestBuilder SetConditionExpression(string expression)
        {
            ConditionExpression = expression;
            return this;
        }
    }
}