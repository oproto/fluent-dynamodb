using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithKeyExtensionsTests
{
    private readonly TestBuilder _builder = new();

    [Fact]
    public void WithKey_AttributeValue_PrimaryKeyOnly_ShouldSetKey()
    {
        // Arrange
        var primaryKeyValue = new AttributeValue { S = "USER#123" };

        // Act
        var result = _builder.WithKey("pk", primaryKeyValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
    }

    [Fact]
    public void WithKey_AttributeValue_PrimaryAndSortKey_ShouldSetBothKeys()
    {
        // Arrange
        var primaryKeyValue = new AttributeValue { S = "USER#123" };
        var sortKeyValue = new AttributeValue { S = "ORDER#456" };

        // Act
        var result = _builder.WithKey("pk", primaryKeyValue, "sk", sortKeyValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(2);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
        _builder.KeyValues["sk"].S.Should().Be("ORDER#456");
    }

    [Fact]
    public void WithKey_AttributeValue_SortKeyNameButNullValue_ShouldSetPrimaryKeyOnly()
    {
        // Arrange
        var primaryKeyValue = new AttributeValue { S = "USER#123" };

        // Act
        var result = _builder.WithKey("pk", primaryKeyValue, "sk", null);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
        _builder.KeyValues.Should().NotContainKey("sk");
    }

    [Fact]
    public void WithKey_AttributeValue_NullSortKeyName_ShouldSetPrimaryKeyOnly()
    {
        // Arrange
        var primaryKeyValue = new AttributeValue { S = "USER#123" };
        var sortKeyValue = new AttributeValue { S = "ORDER#456" };

        // Act
        var result = _builder.WithKey("pk", primaryKeyValue, null, sortKeyValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
        _builder.KeyValues.Should().NotContainKey("sk");
    }

    [Fact]
    public void WithKey_String_SingleKey_ShouldSetStringKey()
    {
        // Act
        var result = _builder.WithKey("pk", "USER#123");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
    }

    [Fact]
    public void WithKey_String_PrimaryAndSortKey_ShouldSetBothStringKeys()
    {
        // Act
        var result = _builder.WithKey("pk", "USER#123", "sk", "ORDER#456");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(2);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
        _builder.KeyValues["sk"].S.Should().Be("ORDER#456");
    }

    [Fact]
    public void WithKey_String_EmptyValues_ShouldSetEmptyStringKeys()
    {
        // Act
        var result = _builder.WithKey("pk", "", "sk", "");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(2);
        _builder.KeyValues["pk"].S.Should().Be("");
        _builder.KeyValues["sk"].S.Should().Be("");
    }

    [Fact]
    public void WithKey_String_EmptyKeyNames_ShouldSetKeysWithEmptyNames()
    {
        // Act
        var result = _builder.WithKey("", "value1", "", "value2");

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1); // Second empty key name overwrites the first
        _builder.KeyValues[""].S.Should().Be("value2");
    }

    [Fact]
    public void WithKey_NumericAttributeValue_ShouldSetNumericKey()
    {
        // Arrange
        var numericKeyValue = new AttributeValue { N = "123" };

        // Act
        var result = _builder.WithKey("id", numericKeyValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["id"].N.Should().Be("123");
    }

    [Fact]
    public void WithKey_BinaryAttributeValue_ShouldSetBinaryKey()
    {
        // Arrange
        var binaryData = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var binaryKeyValue = new AttributeValue { B = binaryData };

        // Act
        var result = _builder.WithKey("data", binaryKeyValue);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(1);
        _builder.KeyValues["data"].B.Should().BeSameAs(binaryData);
    }

    [Fact]
    public void WithKey_MixedAttributeValueTypes_ShouldSetMixedKeys()
    {
        // Arrange
        var stringKey = new AttributeValue { S = "USER#123" };
        var numericKey = new AttributeValue { N = "456" };

        // Act
        var result = _builder.WithKey("pk", stringKey, "sk", numericKey);

        // Assert
        result.Should().BeSameAs(_builder);
        _builder.KeyValues.Should().HaveCount(2);
        _builder.KeyValues["pk"].S.Should().Be("USER#123");
        _builder.KeyValues["sk"].N.Should().Be("456");
    }

    // Test builder class for testing extension methods
    private class TestBuilder : IWithKey<TestBuilder>
    {
        public Dictionary<string, AttributeValue> KeyValues { get; } = new();
        public TestBuilder Self => this;

        public TestBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyFunc)
        {
            keyFunc(KeyValues);
            return this;
        }
    }
}