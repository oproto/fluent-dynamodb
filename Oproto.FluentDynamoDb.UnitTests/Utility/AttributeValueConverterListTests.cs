using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class AttributeValueConverterListTests
{
    #region ToList Tests - String Elements

    [Fact]
    public void ToList_StringList_NonEmpty_ShouldReturnListAttributeValue()
    {
        // Arrange
        var list = new List<string> { "first", "second", "third" };

        // Act
        var result = AttributeValueConverter.ToList(list, s => new AttributeValue { S = s });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().NotBeNull();
        result.L.Should().HaveCount(3);
        result.L[0].S.Should().Be("first");
        result.L[1].S.Should().Be("second");
        result.L[2].S.Should().Be("third");
    }

    [Fact]
    public void ToList_StringList_Empty_ShouldReturnNull()
    {
        // Arrange
        var list = new List<string>();

        // Act
        var result = AttributeValueConverter.ToList(list, s => new AttributeValue { S = s });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToList_StringList_Null_ShouldReturnNull()
    {
        // Arrange
        List<string>? list = null;

        // Act
        var result = AttributeValueConverter.ToList(list, s => new AttributeValue { S = s });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToList_StringList_SingleElement_ShouldReturnListWithOneElement()
    {
        // Arrange
        var list = new List<string> { "onlyElement" };

        // Act
        var result = AttributeValueConverter.ToList(list, s => new AttributeValue { S = s });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(1);
        result.L[0].S.Should().Be("onlyElement");
    }

    [Fact]
    public void ToList_StringList_PreservesOrder()
    {
        // Arrange
        var list = new List<string> { "z", "a", "m", "b" };

        // Act
        var result = AttributeValueConverter.ToList(list, s => new AttributeValue { S = s });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(4);
        result.L[0].S.Should().Be("z");
        result.L[1].S.Should().Be("a");
        result.L[2].S.Should().Be("m");
        result.L[3].S.Should().Be("b");
    }

    #endregion

    #region ToList Tests - Numeric Elements

    [Fact]
    public void ToList_IntList_NonEmpty_ShouldReturnListAttributeValue()
    {
        // Arrange
        var list = new List<int> { 10, 20, 30, 40 };

        // Act
        var result = AttributeValueConverter.ToList(list, n => new AttributeValue { N = n.ToString() });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(4);
        result.L[0].N.Should().Be("10");
        result.L[1].N.Should().Be("20");
        result.L[2].N.Should().Be("30");
        result.L[3].N.Should().Be("40");
    }

    [Fact]
    public void ToList_DecimalList_NonEmpty_ShouldReturnListAttributeValue()
    {
        // Arrange
        var list = new List<decimal> { 10.5m, 20.75m, 30.25m };

        // Act
        var result = AttributeValueConverter.ToList(list, d => new AttributeValue { N = d.ToString() });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(3);
        result.L[0].N.Should().Be("10.5");
        result.L[1].N.Should().Be("20.75");
        result.L[2].N.Should().Be("30.25");
    }

    [Fact]
    public void ToList_IntList_WithNegativeNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var list = new List<int> { -100, -50, 0, 50, 100 };

        // Act
        var result = AttributeValueConverter.ToList(list, n => new AttributeValue { N = n.ToString() });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(5);
        result.L[0].N.Should().Be("-100");
        result.L[1].N.Should().Be("-50");
        result.L[2].N.Should().Be("0");
        result.L[3].N.Should().Be("50");
        result.L[4].N.Should().Be("100");
    }

    #endregion

    #region ToList Tests - Boolean Elements

    [Fact]
    public void ToList_BoolList_NonEmpty_ShouldReturnListAttributeValue()
    {
        // Arrange
        var list = new List<bool> { true, false, true, true };

        // Act
        var result = AttributeValueConverter.ToList(list, b => new AttributeValue { BOOL = b });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(4);
        result.L[0].BOOL.Should().BeTrue();
        result.L[1].BOOL.Should().BeFalse();
        result.L[2].BOOL.Should().BeTrue();
        result.L[3].BOOL.Should().BeTrue();
    }

    #endregion

    #region ToList Tests - Complex Elements

    [Fact]
    public void ToList_ComplexObjectList_ShouldConvertUsingCustomConverter()
    {
        // Arrange
        var list = new List<TestObject>
        {
            new TestObject { Id = "1", Name = "First" },
            new TestObject { Id = "2", Name = "Second" }
        };

        // Act
        var result = AttributeValueConverter.ToList(list, obj => new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = obj.Id } },
                { "name", new AttributeValue { S = obj.Name } }
            }
        });

        // Assert
        result.Should().NotBeNull();
        result!.L.Should().HaveCount(2);
        result.L[0].M["id"].S.Should().Be("1");
        result.L[0].M["name"].S.Should().Be("First");
        result.L[1].M["id"].S.Should().Be("2");
        result.L[1].M["name"].S.Should().Be("Second");
    }

    #endregion

    #region FromList Tests - String Elements

    [Fact]
    public void FromList_StringList_ValidAttributeValue_ShouldReconstructList()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue { S = "apple" },
                new AttributeValue { S = "banana" },
                new AttributeValue { S = "cherry" }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Should().Be("apple");
        result[1].Should().Be("banana");
        result[2].Should().Be("cherry");
    }

    [Fact]
    public void FromList_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromList_AttributeValueWithNullList_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notAList" };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromList_EmptyList_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { L = new List<AttributeValue>() };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromList_StringList_PreservesOrder()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue { S = "z" },
                new AttributeValue { S = "a" },
                new AttributeValue { S = "m" },
                new AttributeValue { S = "b" }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result![0].Should().Be("z");
        result[1].Should().Be("a");
        result[2].Should().Be("m");
        result[3].Should().Be("b");
    }

    #endregion

    #region FromList Tests - Numeric Elements

    [Fact]
    public void FromList_IntList_ValidAttributeValue_ShouldReconstructList()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue { N = "100" },
                new AttributeValue { N = "200" },
                new AttributeValue { N = "300" }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => int.Parse(av.N));

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Should().Be(100);
        result[1].Should().Be(200);
        result[2].Should().Be(300);
    }

    [Fact]
    public void FromList_DecimalList_ValidAttributeValue_ShouldReconstructList()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue { N = "10.5" },
                new AttributeValue { N = "20.75" },
                new AttributeValue { N = "30.25" }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => decimal.Parse(av.N));

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Should().Be(10.5m);
        result[1].Should().Be(20.75m);
        result[2].Should().Be(30.25m);
    }

    #endregion

    #region FromList Tests - Boolean Elements

    [Fact]
    public void FromList_BoolList_ValidAttributeValue_ShouldReconstructList()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue { BOOL = true },
                new AttributeValue { BOOL = false },
                new AttributeValue { BOOL = true }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => av.BOOL);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Should().BeTrue();
        result[1].Should().BeFalse();
        result[2].Should().BeTrue();
    }

    #endregion

    #region FromList Tests - Complex Elements

    [Fact]
    public void FromList_ComplexObjectList_ShouldReconstructUsingCustomConverter()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "1" } },
                        { "name", new AttributeValue { S = "First" } }
                    }
                },
                new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        { "id", new AttributeValue { S = "2" } },
                        { "name", new AttributeValue { S = "Second" } }
                    }
                }
            }
        };

        // Act
        var result = AttributeValueConverter.FromList(attributeValue, av => new TestObject
        {
            Id = av.M["id"].S,
            Name = av.M["name"].S
        });

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Id.Should().Be("1");
        result[0].Name.Should().Be("First");
        result[1].Id.Should().Be("2");
        result[1].Name.Should().Be("Second");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void ToList_FromList_StringRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new List<string> { "alpha", "beta", "gamma", "delta" };

        // Act
        var attributeValue = AttributeValueConverter.ToList(originalList, s => new AttributeValue { S = s });
        var reconstructedList = AttributeValueConverter.FromList(attributeValue, av => av.S);

        // Assert
        reconstructedList.Should().NotBeNull();
        reconstructedList.Should().BeEquivalentTo(originalList, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ToList_FromList_IntRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new List<int> { 5, 10, 15, 20, 25 };

        // Act
        var attributeValue = AttributeValueConverter.ToList(originalList, n => new AttributeValue { N = n.ToString() });
        var reconstructedList = AttributeValueConverter.FromList(attributeValue, av => int.Parse(av.N));

        // Assert
        reconstructedList.Should().NotBeNull();
        reconstructedList.Should().BeEquivalentTo(originalList, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ToList_FromList_DecimalRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new List<decimal> { 99.99m, 199.99m, 299.99m };

        // Act
        var attributeValue = AttributeValueConverter.ToList(originalList, d => new AttributeValue { N = d.ToString() });
        var reconstructedList = AttributeValueConverter.FromList(attributeValue, av => decimal.Parse(av.N));

        // Assert
        reconstructedList.Should().NotBeNull();
        reconstructedList.Should().BeEquivalentTo(originalList, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ToList_FromList_BoolRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new List<bool> { true, false, true, false, true };

        // Act
        var attributeValue = AttributeValueConverter.ToList(originalList, b => new AttributeValue { BOOL = b });
        var reconstructedList = AttributeValueConverter.FromList(attributeValue, av => av.BOOL);

        // Assert
        reconstructedList.Should().NotBeNull();
        reconstructedList.Should().BeEquivalentTo(originalList, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ToList_FromList_ComplexObjectRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalList = new List<TestObject>
        {
            new TestObject { Id = "1", Name = "First" },
            new TestObject { Id = "2", Name = "Second" },
            new TestObject { Id = "3", Name = "Third" }
        };

        // Act
        var attributeValue = AttributeValueConverter.ToList(originalList, obj => new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = obj.Id } },
                { "name", new AttributeValue { S = obj.Name } }
            }
        });
        var reconstructedList = AttributeValueConverter.FromList(attributeValue, av => new TestObject
        {
            Id = av.M["id"].S,
            Name = av.M["name"].S
        });

        // Assert
        reconstructedList.Should().NotBeNull();
        reconstructedList.Should().HaveCount(3);
        for (int i = 0; i < originalList.Count; i++)
        {
            reconstructedList![i].Id.Should().Be(originalList[i].Id);
            reconstructedList[i].Name.Should().Be(originalList[i].Name);
        }
    }

    #endregion

    #region Test Helper Classes

    private class TestObject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
