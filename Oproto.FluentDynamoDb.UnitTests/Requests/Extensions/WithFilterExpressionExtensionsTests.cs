using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithFilterExpressionExtensionsTests
{
    private readonly TestBuilder _builder;

    public WithFilterExpressionExtensionsTests()
    {
        _builder = new TestBuilder();
    }

    [Fact]
    public void WithFilter_WithSimpleExpression_SetsExpressionDirectly()
    {
        // Arrange
        var expression = "#status = :status";

        // Act
        var result = _builder.WithFilter(expression);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be(expression);
    }

    [Fact]
    public void WithFilter_WithFormatString_ProcessesParametersCorrectly()
    {
        // Arrange
        var format = "#status = {0} AND #amount > {1}";
        var status = "ACTIVE";
        var amount = 100;

        // Act
        var result = _builder.WithFilter(format, status, amount);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#status = :p0 AND #amount > :p1");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(status);
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p1")
            .WhoseValue.N.Should().Be("100");
    }

    [Fact]
    public void WithFilter_WithDateTimeFormatting_FormatsCorrectly()
    {
        // Arrange
        var format = "#created > {0:o}";
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _builder.WithFilter(format, dateTime);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#created > :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be("2024-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void WithFilter_WithNumericFormatting_FormatsCorrectly()
    {
        // Arrange
        var format = "#amount BETWEEN {0:F2} AND {1:F2}";
        var minAmount = 10.999m;
        var maxAmount = 99.999m;

        // Act
        var result = _builder.WithFilter(format, minAmount, maxAmount);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#amount BETWEEN :p0 AND :p1");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.N.Should().Be("11.00"); // Decimal rounds 10.999 to 11.00 with F2 format
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p1")
            .WhoseValue.N.Should().Be("100.00"); // Decimal rounds 99.999 to 100.00 with F2 format
    }

    [Fact]
    public void WithFilter_WithBooleanValue_ConvertsCorrectly()
    {
        // Arrange
        var format = "#active = {0}";
        var active = true;

        // Act
        var result = _builder.WithFilter(format, active);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#active = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.BOOL.Should().Be(active);
    }

    [Fact]
    public void WithFilter_WithEnumValue_ConvertsToString()
    {
        // Arrange
        var format = "#status = {0}";
        var status = TestEnum.Active;

        // Act
        var result = _builder.WithFilter(format, status);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#status = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be("Active");
    }

    [Fact]
    public void WithFilter_WithComplexConditions_ProcessesAllParameters()
    {
        // Arrange
        var format = "#status = {0} AND begins_with(#name, {1}) AND #created BETWEEN {2:o} AND {3:o}";
        var status = "ACTIVE";
        var namePrefix = "John";
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = _builder.WithFilter(format, status, namePrefix, startDate, endDate);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#status = :p0 AND begins_with(#name, :p1) AND #created BETWEEN :p2 AND :p3");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(4);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be(status);
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be(namePrefix);
        _builder.AttributeValueHelper.AttributeValues[":p2"].S.Should().Be("2024-01-01T00:00:00.0000000Z");
        _builder.AttributeValueHelper.AttributeValues[":p3"].S.Should().Be("2024-12-31T23:59:59.0000000Z");
    }

    [Fact]
    public void WithFilter_WithContainsFunction_ProcessesCorrectly()
    {
        // Arrange
        var format = "contains(#tags, {0}) AND #score > {1}";
        var tag = "important";
        var minScore = 85;

        // Act
        var result = _builder.WithFilter(format, tag, minScore);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("contains(#tags, :p0) AND #score > :p1");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(tag);
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p1")
            .WhoseValue.N.Should().Be("85");
    }

    [Fact]
    public void WithFilter_WithSizeFunction_ProcessesCorrectly()
    {
        // Arrange
        var format = "size(#items) > {0} AND attribute_type(#data, {1})";
        var minSize = 5;
        var attributeType = "S";

        // Act
        var result = _builder.WithFilter(format, minSize, attributeType);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("size(#items) > :p0 AND attribute_type(#data, :p1)");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.N.Should().Be("5");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p1")
            .WhoseValue.S.Should().Be(attributeType);
    }

    [Fact]
    public void WithFilter_WithNullValue_HandlesCorrectly()
    {
        // Arrange
        var format = "#optional = {0}";
        string? nullValue = null;

        // Act
        var result = _builder.WithFilter(format, nullValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#optional = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.NULL.Should().Be(true);
    }

    [Fact]
    public void WithFilter_WithNoPlaceholders_ReturnsExpressionUnchanged()
    {
        // Arrange
        var expression = "attribute_exists(#name) AND attribute_not_exists(#deleted)";

        // Act
        var result = _builder.WithFilter(expression);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be(expression);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithFilter_WithEmptyFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter("", "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void WithFilter_WithNullFormat_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter(null!, "value");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void WithFilter_WithNullArgs_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter("#status = {0}", null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("args");
    }

    [Fact]
    public void WithFilter_WithMismatchedParameterCount_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter("#status = {0} AND #amount > {1}", "ACTIVE");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*references parameter index 1 but only 1 arguments were provided*");
    }

    [Fact]
    public void WithFilter_WithInvalidFormatSpecifier_HandlesGracefully()
    {
        // Arrange - using a format that will be handled by ToString() fallback
        var format = "#name = {0:invalid}";
        var name = "John";

        // Act
        var result = _builder.WithFilter(format, name);

        // Assert - should not throw, but use ToString() fallback
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#name = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0");
    }

    [Fact]
    public void WithFilter_WithUnmatchedBraces_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter("#status = {0", "ACTIVE");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains unmatched braces.*");
    }

    [Fact]
    public void WithFilter_WithNegativeParameterIndex_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _builder.WithFilter("#status = {-1}", "ACTIVE");
        action.Should().Throw<FormatException>()
            .WithMessage("Format string contains invalid parameter indices: -1.*");
    }

    [Fact]
    public void WithFilter_WithMixedTraditionalAndFormatParameters_WorksCorrectly()
    {
        // Arrange
        var format = "#status = {0} AND #customField = :customValue";
        var status = "ACTIVE";

        // Act
        var result = _builder.WithFilter(format, status);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#status = :p0 AND #customField = :customValue");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(status);
        // Note: :customValue would be added separately via WithValue()
    }

    [Fact]
    public void WithFilter_WithComplexFilterExpression_ProcessesCorrectly()
    {
        // Arrange
        var format = "(#status = {0} OR #status = {1}) AND #amount BETWEEN {2:F2} AND {3:F2} AND contains(#tags, {4}) AND #created > {5:o}";
        var status1 = "ACTIVE";
        var status2 = "PENDING";
        var minAmount = 10.5m;
        var maxAmount = 100.75m;
        var tag = "important";
        var createdAfter = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _builder.WithFilter(format, status1, status2, minAmount, maxAmount, tag, createdAfter);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("(#status = :p0 OR #status = :p1) AND #amount BETWEEN :p2 AND :p3 AND contains(#tags, :p4) AND #created > :p5");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(6);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be(status1);
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be(status2);
        _builder.AttributeValueHelper.AttributeValues[":p2"].N.Should().Be("10.50");
        _builder.AttributeValueHelper.AttributeValues[":p3"].N.Should().Be("100.75");
        _builder.AttributeValueHelper.AttributeValues[":p4"].S.Should().Be(tag);
        _builder.AttributeValueHelper.AttributeValues[":p5"].S.Should().Be("2024-01-01T00:00:00.0000000Z");
    }

    [Fact]
    public void WithFilter_WithAttributeExistsAndNotExists_ProcessesCorrectly()
    {
        // Arrange
        var format = "attribute_exists(#field1) AND attribute_not_exists(#field2) AND #status = {0}";
        var status = "ACTIVE";

        // Act
        var result = _builder.WithFilter(format, status);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("attribute_exists(#field1) AND attribute_not_exists(#field2) AND #status = :p0");
        _builder.AttributeValueHelper.AttributeValues.Should().ContainKey(":p0")
            .WhoseValue.S.Should().Be(status);
    }

    [Fact]
    public void WithFilter_WithInFunction_ProcessesCorrectly()
    {
        // Arrange
        var format = "#status IN ({0}, {1}, {2})";
        var status1 = "ACTIVE";
        var status2 = "PENDING";
        var status3 = "COMPLETED";

        // Act
        var result = _builder.WithFilter(format, status1, status2, status3);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.FilterExpression.Should().Be("#status IN (:p0, :p1, :p2)");
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(3);
        _builder.AttributeValueHelper.AttributeValues[":p0"].S.Should().Be(status1);
        _builder.AttributeValueHelper.AttributeValues[":p1"].S.Should().Be(status2);
        _builder.AttributeValueHelper.AttributeValues[":p2"].S.Should().Be(status3);
    }

    private enum TestEnum
    {
        Active,
        Inactive
    }

    private class TestBuilder : IWithFilterExpression<TestBuilder>
    {
        public AttributeValueInternal AttributeValueHelper { get; } = new();
        public AttributeNameInternal AttributeNameHelper { get; } = new();
        public string? FilterExpression { get; private set; }

        public AttributeValueInternal GetAttributeValueHelper() => AttributeValueHelper;
        public AttributeNameInternal GetAttributeNameHelper() => AttributeNameHelper;

        public TestBuilder SetFilterExpression(string expression)
        {
            FilterExpression = expression;
            return this;
        }

        public TestBuilder Self => this;
    }
}