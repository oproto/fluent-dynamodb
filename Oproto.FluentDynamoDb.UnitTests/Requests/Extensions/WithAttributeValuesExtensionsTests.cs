using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithAttributeValuesExtensionsTests
{
    private readonly TestBuilder _builder = new();

    [Fact]
    public void WithValues_Dictionary_ShouldAddAllValues()
    {
        // Arrange
        var values = new Dictionary<string, AttributeValue>
        {
            { ":pk", new AttributeValue { S = "test" } },
            { ":sk", new AttributeValue { N = "123" } }
        };

        // Act
        var result = _builder.WithValues(values);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":pk"].S.Should().Be("test");
        _builder.AttributeValueHelper.AttributeValues[":sk"].N.Should().Be("123");
    }

    [Fact]
    public void WithValues_Action_ShouldConfigureValues()
    {
        // Act
        var result = _builder.WithValues(values =>
        {
            values.Add(":pk", new AttributeValue { S = "test" });
            values.Add(":sk", new AttributeValue { N = "456" });
        });

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":pk"].S.Should().Be("test");
        _builder.AttributeValueHelper.AttributeValues[":sk"].N.Should().Be("456");
    }

    [Fact]
    public void WithValue_String_ShouldAddStringValue()
    {
        // Act
        var result = _builder.WithValue(":name", "John Doe");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":name"].S.Should().Be("John Doe");
    }

    [Fact]
    public void WithValue_String_Null_ConditionalUseTrue_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":name", null as string, conditionalUse: true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_String_Null_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":name", null as string, conditionalUse: false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_Boolean_True_ShouldAddBooleanValue()
    {
        // Act
        var result = _builder.WithValue(":active", true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":active"].BOOL.Should().BeTrue();
        _builder.AttributeValueHelper.AttributeValues[":active"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void WithValue_Boolean_False_ShouldAddBooleanValue()
    {
        // Act
        var result = _builder.WithValue(":active", false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":active"].BOOL.Should().BeFalse();
        _builder.AttributeValueHelper.AttributeValues[":active"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void WithValue_Boolean_Null_ConditionalUseTrue_ShouldAddBooleanValue()
    {
        // Act
        var result = _builder.WithValue(":active", (bool?)null, conditionalUse: true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":active"].BOOL.Should().BeNull();
        _builder.AttributeValueHelper.AttributeValues[":active"].IsBOOLSet.Should().BeFalse();
    }

    [Fact]
    public void WithValue_Boolean_Null_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":active", (bool?)null, conditionalUse: false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_Decimal_ShouldAddNumericValue()
    {
        // Act
        var result = _builder.WithValue(":amount", 123.45m);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":amount"].N.Should().Be("123.45");
    }

    [Fact]
    public void WithValue_Decimal_Null_ConditionalUseTrue_ShouldAddNumericValue()
    {
        // Act
        var result = _builder.WithValue(":amount", (decimal?)null, conditionalUse: true);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":amount"].N.Should().Be("");
    }

    [Fact]
    public void WithValue_Decimal_Null_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":amount", (decimal?)null, conditionalUse: false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_StringDictionary_ShouldAddMapValue()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        var result = _builder.WithValue(":metadata", dict);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":metadata"].M.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":metadata"].M["key1"].S.Should().Be("value1");
        _builder.AttributeValueHelper.AttributeValues[":metadata"].M["key2"].S.Should().Be("value2");
    }

    [Fact]
    public void WithValue_StringDictionary_Null_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":metadata", null as Dictionary<string, string>, conditionalUse: false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_AttributeValueDictionary_ShouldAddMapValue()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>
        {
            { "key1", new AttributeValue { S = "value1" } },
            { "key2", new AttributeValue { N = "123" } }
        };

        // Act
        var result = _builder.WithValue(":complex", dict);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().HaveCount(1);
        _builder.AttributeValueHelper.AttributeValues[":complex"].M.Should().HaveCount(2);
        _builder.AttributeValueHelper.AttributeValues[":complex"].M["key1"].S.Should().Be("value1");
        _builder.AttributeValueHelper.AttributeValues[":complex"].M["key2"].N.Should().Be("123");
    }

    [Fact]
    public void WithValue_AttributeValueDictionary_Null_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        var result = _builder.WithValue(":complex", null as Dictionary<string, AttributeValue>, conditionalUse: false);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.AttributeValueHelper.AttributeValues.Should().BeEmpty();
    }

    // Test builder class for testing extension methods
    private class TestBuilder : IWithAttributeValues<TestBuilder>
    {
        public AttributeValueInternal AttributeValueHelper { get; } = new();
        public TestBuilder Self => this;

        public AttributeValueInternal GetAttributeValueHelper() => AttributeValueHelper;
    }
}