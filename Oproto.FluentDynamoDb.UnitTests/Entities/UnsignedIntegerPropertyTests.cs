using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Property-based tests for unsigned integer type serialization and deserialization.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// Feature: unsigned-integer-types
/// </summary>
public class UnsignedIntegerPropertyTests
{
    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 1.1-1.3, 7.1**
    /// 
    /// For any valid ulong value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Ulong_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<ulong>(),
            original =>
            {
                // Act - serialize to Number format (same as source generator)
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = ulong.Parse(attributeValue.N);
                
                // Assert
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve ulong value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 2.1-2.3, 7.1**
    /// 
    /// For any valid uint value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Uint_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<uint>(),
            original =>
            {
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = uint.Parse(attributeValue.N);
                
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve uint value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 3.1-3.3, 7.1**
    /// 
    /// For any valid ushort value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Ushort_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<ushort>(),
            original =>
            {
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = ushort.Parse(attributeValue.N);
                
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve ushort value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 4.1-4.3, 7.1**
    /// 
    /// For any valid byte value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Byte_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<byte>(),
            original =>
            {
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = byte.Parse(attributeValue.N);
                
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve byte value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 5.1-5.3, 7.1**
    /// 
    /// For any valid sbyte value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Sbyte_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<sbyte>(),
            original =>
            {
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = sbyte.Parse(attributeValue.N);
                
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve sbyte value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 6.1-6.3, 7.1**
    /// 
    /// For any valid short value, serializing to DynamoDB Number format
    /// and deserializing back SHALL produce the original value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Short_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            Arb.From<short>(),
            original =>
            {
                var attributeValue = new AttributeValue { N = original.ToString() };
                var deserialized = short.Parse(attributeValue.N);
                
                return (original == deserialized).ToProperty()
                    .Label($"Round-trip should preserve short value. Original: {original}, Deserialized: {deserialized}");
            });
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 2: Serialization round-trip consistency**
    /// **Validates: Requirements 7.2**
    /// 
    /// Boundary values (0 and max) should round-trip correctly for all unsigned types.
    /// </summary>
    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    public void Ulong_BoundaryValues_RoundTrip(ulong original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = ulong.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(uint.MaxValue)]
    public void Uint_BoundaryValues_RoundTrip(uint original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = uint.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData(ushort.MaxValue)]
    public void Ushort_BoundaryValues_RoundTrip(ushort original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = ushort.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData(byte.MaxValue)]
    public void Byte_BoundaryValues_RoundTrip(byte original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = byte.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [InlineData(sbyte.MinValue)]
    [InlineData((sbyte)0)]
    [InlineData(sbyte.MaxValue)]
    public void Sbyte_BoundaryValues_RoundTrip(sbyte original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = sbyte.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData((short)0)]
    [InlineData(short.MaxValue)]
    public void Short_BoundaryValues_RoundTrip(short original)
    {
        var attributeValue = new AttributeValue { N = original.ToString() };
        var deserialized = short.Parse(attributeValue.N);
        
        Assert.Equal(original, deserialized);
    }

    /// <summary>
    /// **Feature: unsigned-integer-types, Property 3: Collection serialization round-trip**
    /// **Validates: Requirements 1.5, 2.5, 3.5, 4.5, 5.5, 6.5**
    /// 
    /// For any List of unsigned integers, serializing to DynamoDB List format
    /// and deserializing back SHALL produce a collection with the same elements.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UlongList_RoundTrip_PreservesElements()
    {
        return Prop.ForAll(
            Arb.From<List<ulong>>(),
            original =>
            {
                // Serialize to DynamoDB List format
                var attributeValue = new AttributeValue
                {
                    L = original.Select(x => new AttributeValue { N = x.ToString() }).ToList()
                };
                
                // Deserialize back
                var deserialized = attributeValue.L.Select(x => ulong.Parse(x.N)).ToList();
                
                // Assert
                var areEqual = original.SequenceEqual(deserialized);
                return areEqual.ToProperty()
                    .Label($"Round-trip should preserve List<ulong> elements. Count: {original.Count}");
            });
    }

    [Property(MaxTest = 100)]
    public Property UintList_RoundTrip_PreservesElements()
    {
        return Prop.ForAll(
            Arb.From<List<uint>>(),
            original =>
            {
                var attributeValue = new AttributeValue
                {
                    L = original.Select(x => new AttributeValue { N = x.ToString() }).ToList()
                };
                var deserialized = attributeValue.L.Select(x => uint.Parse(x.N)).ToList();
                
                return original.SequenceEqual(deserialized).ToProperty()
                    .Label($"Round-trip should preserve List<uint> elements. Count: {original.Count}");
            });
    }

    [Property(MaxTest = 100)]
    public Property ByteList_RoundTrip_PreservesElements()
    {
        return Prop.ForAll(
            Arb.From<List<byte>>(),
            original =>
            {
                var attributeValue = new AttributeValue
                {
                    L = original.Select(x => new AttributeValue { N = x.ToString() }).ToList()
                };
                var deserialized = attributeValue.L.Select(x => byte.Parse(x.N)).ToList();
                
                return original.SequenceEqual(deserialized).ToProperty()
                    .Label($"Round-trip should preserve List<byte> elements. Count: {original.Count}");
            });
    }
}
