using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class AttributeValueConverterSetTests
{
    #region ToStringSet Tests

    [Fact]
    public void ToStringSet_NonEmptyHashSet_ShouldReturnStringSetAttributeValue()
    {
        // Arrange
        var set = new HashSet<string> { "value1", "value2", "value3" };

        // Act
        var result = AttributeValueConverter.ToStringSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.SS.Should().NotBeNull();
        result.SS.Should().HaveCount(3);
        result.SS.Should().Contain("value1");
        result.SS.Should().Contain("value2");
        result.SS.Should().Contain("value3");
    }

    [Fact]
    public void ToStringSet_EmptyHashSet_ShouldReturnNull()
    {
        // Arrange
        var set = new HashSet<string>();

        // Act
        var result = AttributeValueConverter.ToStringSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToStringSet_Null_ShouldReturnNull()
    {
        // Arrange
        HashSet<string>? set = null;

        // Act
        var result = AttributeValueConverter.ToStringSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToStringSet_SingleElement_ShouldReturnStringSetWithOneElement()
    {
        // Arrange
        var set = new HashSet<string> { "onlyValue" };

        // Act
        var result = AttributeValueConverter.ToStringSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.SS.Should().HaveCount(1);
        result.SS.Should().Contain("onlyValue");
    }

    #endregion

    #region ToNumberSet Int Tests

    [Fact]
    public void ToNumberSet_IntHashSet_NonEmpty_ShouldReturnNumberSetAttributeValue()
    {
        // Arrange
        var set = new HashSet<int> { 1, 2, 3, 42, 100 };

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.NS.Should().NotBeNull();
        result.NS.Should().HaveCount(5);
        result.NS.Should().Contain("1");
        result.NS.Should().Contain("2");
        result.NS.Should().Contain("3");
        result.NS.Should().Contain("42");
        result.NS.Should().Contain("100");
    }

    [Fact]
    public void ToNumberSet_IntHashSet_Empty_ShouldReturnNull()
    {
        // Arrange
        var set = new HashSet<int>();

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToNumberSet_IntHashSet_Null_ShouldReturnNull()
    {
        // Arrange
        HashSet<int>? set = null;

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToNumberSet_IntHashSet_NegativeNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var set = new HashSet<int> { -10, -5, 0, 5, 10 };

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.NS.Should().HaveCount(5);
        result.NS.Should().Contain("-10");
        result.NS.Should().Contain("-5");
        result.NS.Should().Contain("0");
        result.NS.Should().Contain("5");
        result.NS.Should().Contain("10");
    }

    #endregion

    #region ToNumberSet Long Tests

    [Fact]
    public void ToNumberSet_LongHashSet_NonEmpty_ShouldReturnNumberSetAttributeValue()
    {
        // Arrange
        var set = new HashSet<long> { 1000000000L, 2000000000L, 3000000000L };

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.NS.Should().NotBeNull();
        result.NS.Should().HaveCount(3);
        result.NS.Should().Contain("1000000000");
        result.NS.Should().Contain("2000000000");
        result.NS.Should().Contain("3000000000");
    }

    [Fact]
    public void ToNumberSet_LongHashSet_Empty_ShouldReturnNull()
    {
        // Arrange
        var set = new HashSet<long>();

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToNumberSet_LongHashSet_Null_ShouldReturnNull()
    {
        // Arrange
        HashSet<long>? set = null;

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ToNumberSet Decimal Tests

    [Fact]
    public void ToNumberSet_DecimalHashSet_NonEmpty_ShouldReturnNumberSetAttributeValue()
    {
        // Arrange
        var set = new HashSet<decimal> { 10.5m, 20.75m, 30.25m };

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.NS.Should().NotBeNull();
        result.NS.Should().HaveCount(3);
        result.NS.Should().Contain("10.5");
        result.NS.Should().Contain("20.75");
        result.NS.Should().Contain("30.25");
    }

    [Fact]
    public void ToNumberSet_DecimalHashSet_Empty_ShouldReturnNull()
    {
        // Arrange
        var set = new HashSet<decimal>();

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToNumberSet_DecimalHashSet_Null_ShouldReturnNull()
    {
        // Arrange
        HashSet<decimal>? set = null;

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToNumberSet_DecimalHashSet_HighPrecision_ShouldPreservePrecision()
    {
        // Arrange
        var set = new HashSet<decimal> { 123.456789m, 987.654321m };

        // Act
        var result = AttributeValueConverter.ToNumberSet(set);

        // Assert
        result.Should().NotBeNull();
        result!.NS.Should().HaveCount(2);
        result.NS.Should().Contain("123.456789");
        result.NS.Should().Contain("987.654321");
    }

    #endregion

    #region ToBinarySet Tests

    [Fact]
    public void ToBinarySet_NonEmptyHashSet_ShouldReturnBinarySetAttributeValue()
    {
        // Arrange
        var set = new HashSet<byte[]>
        {
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            new byte[] { 7, 8, 9 }
        };

        // Act
        var result = AttributeValueConverter.ToBinarySet(set);

        // Assert
        result.Should().NotBeNull();
        result!.BS.Should().NotBeNull();
        result.BS.Should().HaveCount(3);
    }

    [Fact]
    public void ToBinarySet_EmptyHashSet_ShouldReturnNull()
    {
        // Arrange
        var set = new HashSet<byte[]>();

        // Act
        var result = AttributeValueConverter.ToBinarySet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToBinarySet_Null_ShouldReturnNull()
    {
        // Arrange
        HashSet<byte[]>? set = null;

        // Act
        var result = AttributeValueConverter.ToBinarySet(set);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToBinarySet_SingleByteArray_ShouldReturnBinarySetWithOneElement()
    {
        // Arrange
        var set = new HashSet<byte[]>
        {
            new byte[] { 10, 20, 30, 40, 50 }
        };

        // Act
        var result = AttributeValueConverter.ToBinarySet(set);

        // Assert
        result.Should().NotBeNull();
        result!.BS.Should().HaveCount(1);
    }

    #endregion

    #region FromStringSet Tests

    [Fact]
    public void FromStringSet_ValidStringSetAttributeValue_ShouldReconstructHashSet()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            SS = new List<string> { "apple", "banana", "cherry" }
        };

        // Act
        var result = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain("apple");
        result.Should().Contain("banana");
        result.Should().Contain("cherry");
    }

    [Fact]
    public void FromStringSet_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromStringSet_AttributeValueWithNullSS_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notASet" };

        // Act
        var result = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromStringSet_EmptyStringSet_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { SS = new List<string>() };

        // Act
        var result = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FromNumberSetInt Tests

    [Fact]
    public void FromNumberSetInt_ValidNumberSetAttributeValue_ShouldReconstructIntHashSet()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            NS = new List<string> { "10", "20", "30", "40" }
        };

        // Act
        var result = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.Should().Contain(10);
        result.Should().Contain(20);
        result.Should().Contain(30);
        result.Should().Contain(40);
    }

    [Fact]
    public void FromNumberSetInt_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNumberSetInt_EmptyNumberSet_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { NS = new List<string>() };

        // Act
        var result = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNumberSetInt_NegativeNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            NS = new List<string> { "-100", "-50", "0", "50", "100" }
        };

        // Act
        var result = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        result.Should().Contain(-100);
        result.Should().Contain(-50);
        result.Should().Contain(0);
        result.Should().Contain(50);
        result.Should().Contain(100);
    }

    #endregion

    #region FromNumberSetLong Tests

    [Fact]
    public void FromNumberSetLong_ValidNumberSetAttributeValue_ShouldReconstructLongHashSet()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            NS = new List<string> { "1000000000", "2000000000", "3000000000" }
        };

        // Act
        var result = AttributeValueConverter.FromNumberSetLong(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(1000000000L);
        result.Should().Contain(2000000000L);
        result.Should().Contain(3000000000L);
    }

    [Fact]
    public void FromNumberSetLong_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromNumberSetLong(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNumberSetLong_EmptyNumberSet_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { NS = new List<string>() };

        // Act
        var result = AttributeValueConverter.FromNumberSetLong(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FromNumberSetDecimal Tests

    [Fact]
    public void FromNumberSetDecimal_ValidNumberSetAttributeValue_ShouldReconstructDecimalHashSet()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            NS = new List<string> { "10.5", "20.75", "30.25" }
        };

        // Act
        var result = AttributeValueConverter.FromNumberSetDecimal(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(10.5m);
        result.Should().Contain(20.75m);
        result.Should().Contain(30.25m);
    }

    [Fact]
    public void FromNumberSetDecimal_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromNumberSetDecimal(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNumberSetDecimal_EmptyNumberSet_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { NS = new List<string>() };

        // Act
        var result = AttributeValueConverter.FromNumberSetDecimal(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromNumberSetDecimal_HighPrecision_ShouldPreservePrecision()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            NS = new List<string> { "123.456789", "987.654321" }
        };

        // Act
        var result = AttributeValueConverter.FromNumberSetDecimal(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(123.456789m);
        result.Should().Contain(987.654321m);
    }

    #endregion

    #region FromBinarySet Tests

    [Fact]
    public void FromBinarySet_ValidBinarySetAttributeValue_ShouldReconstructHashSet()
    {
        // Arrange
        var attributeValue = new AttributeValue
        {
            BS = new List<MemoryStream>
            {
                new MemoryStream(new byte[] { 1, 2, 3 }),
                new MemoryStream(new byte[] { 4, 5, 6 })
            }
        };

        // Act
        var result = AttributeValueConverter.FromBinarySet(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(arr => arr.SequenceEqual(new byte[] { 1, 2, 3 }));
        result.Should().Contain(arr => arr.SequenceEqual(new byte[] { 4, 5, 6 }));
    }

    [Fact]
    public void FromBinarySet_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromBinarySet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromBinarySet_AttributeValueWithNullBS_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notABinarySet" };

        // Act
        var result = AttributeValueConverter.FromBinarySet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromBinarySet_EmptyBinarySet_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { BS = new List<MemoryStream>() };

        // Act
        var result = AttributeValueConverter.FromBinarySet(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void ToStringSet_FromStringSet_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSet = new HashSet<string> { "alpha", "beta", "gamma", "delta" };

        // Act
        var attributeValue = AttributeValueConverter.ToStringSet(originalSet);
        var reconstructedSet = AttributeValueConverter.FromStringSet(attributeValue);

        // Assert
        reconstructedSet.Should().NotBeNull();
        reconstructedSet.Should().BeEquivalentTo(originalSet);
    }

    [Fact]
    public void ToNumberSet_FromNumberSetInt_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSet = new HashSet<int> { 5, 10, 15, 20, 25 };

        // Act
        var attributeValue = AttributeValueConverter.ToNumberSet(originalSet);
        var reconstructedSet = AttributeValueConverter.FromNumberSetInt(attributeValue);

        // Assert
        reconstructedSet.Should().NotBeNull();
        reconstructedSet.Should().BeEquivalentTo(originalSet);
    }

    [Fact]
    public void ToNumberSet_FromNumberSetLong_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSet = new HashSet<long> { 1000000000L, 2000000000L, 3000000000L };

        // Act
        var attributeValue = AttributeValueConverter.ToNumberSet(originalSet);
        var reconstructedSet = AttributeValueConverter.FromNumberSetLong(attributeValue);

        // Assert
        reconstructedSet.Should().NotBeNull();
        reconstructedSet.Should().BeEquivalentTo(originalSet);
    }

    [Fact]
    public void ToNumberSet_FromNumberSetDecimal_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSet = new HashSet<decimal> { 99.99m, 199.99m, 299.99m };

        // Act
        var attributeValue = AttributeValueConverter.ToNumberSet(originalSet);
        var reconstructedSet = AttributeValueConverter.FromNumberSetDecimal(attributeValue);

        // Assert
        reconstructedSet.Should().NotBeNull();
        reconstructedSet.Should().BeEquivalentTo(originalSet);
    }

    [Fact]
    public void ToBinarySet_FromBinarySet_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSet = new HashSet<byte[]>
        {
            new byte[] { 1, 2, 3, 4, 5 },
            new byte[] { 10, 20, 30, 40, 50 }
        };

        // Act
        var attributeValue = AttributeValueConverter.ToBinarySet(originalSet);
        var reconstructedSet = AttributeValueConverter.FromBinarySet(attributeValue);

        // Assert
        reconstructedSet.Should().NotBeNull();
        reconstructedSet.Should().HaveCount(2);
        reconstructedSet.Should().Contain(arr => arr.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }));
        reconstructedSet.Should().Contain(arr => arr.SequenceEqual(new byte[] { 10, 20, 30, 40, 50 }));
    }

    #endregion
}
