using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class AttributeValueConverterMapTests
{
    #region ToMap Dictionary<string, string> Tests

    [Fact]
    public void ToMap_StringDictionary_NonEmpty_ShouldReturnMapAttributeValue()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", "value3" }
        };

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().NotBeNull();
        result!.M.Should().NotBeNull();
        result.M.Should().HaveCount(3);
        result.M["key1"].S.Should().Be("value1");
        result.M["key2"].S.Should().Be("value2");
        result.M["key3"].S.Should().Be("value3");
    }

    [Fact]
    public void ToMap_StringDictionary_Empty_ShouldReturnNull()
    {
        // Arrange
        var dict = new Dictionary<string, string>();

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToMap_StringDictionary_Null_ShouldReturnNull()
    {
        // Arrange
        Dictionary<string, string>? dict = null;

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToMap_StringDictionary_SingleEntry_ShouldReturnMapWithOneEntry()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "singleKey", "singleValue" }
        };

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().NotBeNull();
        result!.M.Should().HaveCount(1);
        result.M["singleKey"].S.Should().Be("singleValue");
    }

    #endregion

    #region ToMap Dictionary<string, AttributeValue> Tests

    [Fact]
    public void ToMap_AttributeValueDictionary_NonEmpty_ShouldReturnMapAttributeValue()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>
        {
            { "stringKey", new AttributeValue { S = "stringValue" } },
            { "numberKey", new AttributeValue { N = "123" } },
            { "boolKey", new AttributeValue { BOOL = true } }
        };

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().NotBeNull();
        result!.M.Should().NotBeNull();
        result.M.Should().HaveCount(3);
        result.M["stringKey"].S.Should().Be("stringValue");
        result.M["numberKey"].N.Should().Be("123");
        result.M["boolKey"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void ToMap_AttributeValueDictionary_Empty_ShouldReturnNull()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>();

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToMap_AttributeValueDictionary_Null_ShouldReturnNull()
    {
        // Arrange
        Dictionary<string, AttributeValue>? dict = null;

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToMap_AttributeValueDictionary_NestedMap_ShouldPreserveNesting()
    {
        // Arrange
        var nestedMap = new Dictionary<string, AttributeValue>
        {
            { "nestedKey", new AttributeValue { S = "nestedValue" } }
        };
        var dict = new Dictionary<string, AttributeValue>
        {
            { "topLevel", new AttributeValue { S = "topValue" } },
            { "nested", new AttributeValue { M = nestedMap } }
        };

        // Act
        var result = AttributeValueConverter.ToMap(dict);

        // Assert
        result.Should().NotBeNull();
        result!.M.Should().HaveCount(2);
        result.M["topLevel"].S.Should().Be("topValue");
        result.M["nested"].M.Should().NotBeNull();
        result.M["nested"].M["nestedKey"].S.Should().Be("nestedValue");
    }

    #endregion

    #region FromMap Tests

    [Fact]
    public void FromMap_ValidMapAttributeValue_ShouldReconstructDictionary()
    {
        // Arrange
        var map = new Dictionary<string, AttributeValue>
        {
            { "key1", new AttributeValue { S = "value1" } },
            { "key2", new AttributeValue { S = "value2" } },
            { "key3", new AttributeValue { S = "value3" } }
        };
        var attributeValue = new AttributeValue { M = map };

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result!["key1"].Should().Be("value1");
        result["key2"].Should().Be("value2");
        result["key3"].Should().Be("value3");
    }

    [Fact]
    public void FromMap_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromMap_AttributeValueWithNullMap_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notAMap" };

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromMap_EmptyMap_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { M = new Dictionary<string, AttributeValue>() };

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromMap_SingleEntry_ShouldReconstructSingleEntryDictionary()
    {
        // Arrange
        var map = new Dictionary<string, AttributeValue>
        {
            { "onlyKey", new AttributeValue { S = "onlyValue" } }
        };
        var attributeValue = new AttributeValue { M = map };

        // Act
        var result = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result!["onlyKey"].Should().Be("onlyValue");
    }

    #endregion

    #region FromMapRaw Tests

    [Fact]
    public void FromMapRaw_ValidMapAttributeValue_ShouldReturnRawDictionary()
    {
        // Arrange
        var map = new Dictionary<string, AttributeValue>
        {
            { "stringKey", new AttributeValue { S = "stringValue" } },
            { "numberKey", new AttributeValue { N = "456" } }
        };
        var attributeValue = new AttributeValue { M = map };

        // Act
        var result = AttributeValueConverter.FromMapRaw(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!["stringKey"].S.Should().Be("stringValue");
        result["numberKey"].N.Should().Be("456");
    }

    [Fact]
    public void FromMapRaw_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromMapRaw(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromMapRaw_EmptyMap_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { M = new Dictionary<string, AttributeValue>() };

        // Act
        var result = AttributeValueConverter.FromMapRaw(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void ToMap_FromMap_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalDict = new Dictionary<string, string>
        {
            { "firstName", "John" },
            { "lastName", "Doe" },
            { "email", "john.doe@example.com" }
        };

        // Act
        var attributeValue = AttributeValueConverter.ToMap(originalDict);
        var reconstructedDict = AttributeValueConverter.FromMap(attributeValue);

        // Assert
        reconstructedDict.Should().NotBeNull();
        reconstructedDict.Should().BeEquivalentTo(originalDict);
    }

    [Fact]
    public void ToMap_FromMapRaw_RoundTrip_ShouldPreserveComplexData()
    {
        // Arrange
        var originalDict = new Dictionary<string, AttributeValue>
        {
            { "name", new AttributeValue { S = "Product" } },
            { "price", new AttributeValue { N = "99.99" } },
            { "inStock", new AttributeValue { BOOL = true } }
        };

        // Act
        var attributeValue = AttributeValueConverter.ToMap(originalDict);
        var reconstructedDict = AttributeValueConverter.FromMapRaw(attributeValue);

        // Assert
        reconstructedDict.Should().NotBeNull();
        reconstructedDict.Should().HaveCount(3);
        reconstructedDict!["name"].S.Should().Be("Product");
        reconstructedDict["price"].N.Should().Be("99.99");
        reconstructedDict["inStock"].BOOL.Should().BeTrue();
    }

    #endregion
}
