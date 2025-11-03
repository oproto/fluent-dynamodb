using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithUpdateExpressionExtensionsTests
{
    private readonly TestBuilder _builder;

    public WithUpdateExpressionExtensionsTests()
    {
        _builder = new TestBuilder();
    }

    [Fact]
    public void Set_WithSimpleExpression_SetsExpressionDirectly()
    {
        // Arrange
        var expression = "SET #name = :name";

        // Act
        var result = _builder.Set(expression);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be(expression);
    }

    [Fact]
    public void Set_WithFormatString_ProcessesParametersCorrectly()
    {
        // Arrange
        var format = "SET #name = {0}, #status = {1}";
        var name = "John Doe";
        var status = "ACTIVE";

        // Act
        var result = _builder.Set(format, name, status);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #name = :p0, #status = :p1");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(name);
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p1")
            .WhoseValue.S.Should().Be(status);
    }

    [Fact]
    public void Set_WithDateTimeFormatting_FormatsCorrectly()
    {
        // Arrange
        var format = "SET #updated = {0:o}";
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _builder.Set(format, dateTime);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #updated = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be("2024-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void Set_WithNumericFormatting_FormatsCorrectly()
    {
        // Arrange
        var format = "ADD #amount {0:F2}";
        var amount = 99.999m;

        // Act
        var result = _builder.Set(format, amount);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("ADD #amount :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.N.Should().Be("100.00"); // Decimal rounds 99.999 to 100.00 with F2 format
    }

    [Fact]
    public void Set_WithBooleanValue_ConvertsCorrectly()
    {
        // Arrange
        var format = "SET #active = {0}";
        var active = true;

        // Act
        var result = _builder.Set(format, active);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #active = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.BOOL.Should().Be(active);
    }

    [Fact]
    public void Set_WithEnumValue_ConvertsToString()
    {
        // Arrange
        var format = "SET #status = {0}";
        var status = TestEnum.Active;

        // Act
        var result = _builder.Set(format, status);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #status = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be("Active");
    }

    [Fact]
    public void Set_WithMultipleOperations_ProcessesAllParameters()
    {
        // Arrange
        var format = "SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField";
        var name = "John Doe";
        var updated = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var count = 1;

        // Act
        var result = _builder.Set(format, name, updated, count);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #name = :p0, #updated = :p1 ADD #count :p2 REMOVE #oldField");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(3);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be(name);
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
        _builder.AttributeValueHelper.AttributeValues[":p2"].N.Should().Be("1");
    }

    [Fact]
    public void Set_WithNullValue_HandlesCorrectly()
    {
        // Arrange
        var format = "SET #optional = {0}";
        string? nullValue = null;

        // Act
        var result = _builder.Set(format, nullValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #optional = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.NULL.Should().Be(true);
    }

    [Fact]
    public void Set_WithNoPlaceholders_ReturnsExpressionUnchanged()
    {
        // Arrange
        var expression = "REMOVE #oldField, #tempData";

        // Act
        var result = _builder.Set(expression);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be(expression);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Set_WithEmptyFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Set("", "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void Set_WithNullFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Set(null!, "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void Set_WithNullArgs_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => _builder.Set("SET #name = {0}", null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("args");
    }

    [Fact]
    public void Set_WithMismatchedParameterCount_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.Set("SET #name = {0}, #status = {1}", "John");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*references parameter index 1 but only 1 arguments were provided*");
    }

    [Fact]
    public void Set_WithInvalidFormatSpecifier_HandlesGracefully()
    {
        // Arrange - using a format that will be handled by ToString() fallback
        var format = "SET #name = {0:invalid}";
        var name = "John";

        // Act
        var result = _builder.Set(format, name);

        // Assert - should not throw, but use ToString() fallback
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #name = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0");
    }

    [Fact]
    public void Set_WithUnmatchedBraces_ShouldThrowFormatException()
    {
        // Arrange - unmatched braces should throw an exception
        var format = "SET #name = {0";
        var name = "John";

        // Act & Assert - should throw FormatException for unmatched braces
        var action = () => _builder.Set(format, name);
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains unmatched braces.*");
    }

    [Fact]
    public void Set_WithNegativeParameterIndex_ShouldThrowFormatException()
    {
        // Arrange - negative indices should throw an exception
        var format = "SET #name = {-1}";
        var name = "John";

        // Act & Assert - should throw FormatException for negative parameter index
        var action = () => _builder.Set(format, name);
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains invalid parameter indices: -1.*");
    }

    [Fact]
    public void Set_WithMixedTraditionalAndFormatParameters_WorksCorrectly()
    {
        // Arrange
        var format = "SET #name = {0}, #customField = :customValue";
        var name = "John Doe";

        // Act
        var result = _builder.Set(format, name);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #name = :p0, #customField = :customValue");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(name);
        // Note: :customValue would be added separately via WithValue()
    }

    [Fact]
    public void Set_WithComplexUpdateExpression_ProcessesCorrectly()
    {
        // Arrange
        var format = "SET #name = {0}, #updated = {1:o} ADD #count {2}, #tags {3} REMOVE #oldField DELETE #tempSet {4}";
        var name = "John Doe";
        var updated = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var count = 5;
        var tags = new[] { "tag1", "tag2" };
        var tempSet = new[] { "item1" };

        // Act
        var result = _builder.Set(format, name, updated, count, tags, tempSet);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.UpdateExpression.Should().Be("SET #name = :p0, #updated = :p1 ADD #count :p2, #tags :p3 REMOVE #oldField DELETE #tempSet :p4");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(5);
    }

    private enum TestEnum
    {
        Active,
        Inactive
    }

    private class TestBuilder : IWithUpdateExpression<TestBuilder>
    {
        public AttributeValueInternal AttributeValueHelper { get; } = new();
        public string? UpdateExpression { get; private set; }

        public AttributeValueInternal GetAttributeValueHelper() => AttributeValueHelper;

        public TestBuilder SetUpdateExpression(string expression)
        {
            UpdateExpression = expression;
            return this;
        }

        public TestBuilder Self => this;
    }
}