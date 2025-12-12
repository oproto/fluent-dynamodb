using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Property-based tests for DynamicFieldCollection.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class DynamicFieldCollectionPropertyTests
{
    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any valid DynamicFieldCollection, serializing to a dictionary and then
    /// creating a new collection from that dictionary SHALL produce an equivalent collection.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RoundTrip_ToDictionary_PreservesAllFields()
    {
        return Prop.ForAll(
            GenerateDynamicFieldCollection(),
            collection =>
            {
                // Act - serialize to dictionary and create new collection
                var dictionary = collection.ToDictionary();
                var roundTripped = new DynamicFieldCollection(dictionary);
                
                // Assert - all fields should be preserved
                var sameCount = collection.Count == roundTripped.Count;
                var allFieldsPreserved = collection.FieldNames.All(name => 
                    roundTripped.ContainsKey(name) && 
                    AreAttributeValuesEquivalent(collection.GetRaw(name)!, roundTripped.GetRaw(name)!));
                
                return (sameCount && allFieldsPreserved).ToProperty()
                    .Label($"Round-trip should preserve all fields. " +
                           $"SameCount: {sameCount} ({collection.Count}), AllFieldsPreserved: {allFieldsPreserved}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any string value set via SetString, getting it back via GetString SHALL return
    /// the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringRoundTrip_SetThenGet_ReturnsOriginalValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetString(fieldName.Get, value.Get);
                var retrieved = collection.GetString(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved == value.Get;
                
                return valuesMatch.ToProperty()
                    .Label($"String round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any integer value set via SetInt, getting it back via GetInt SHALL return
    /// the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IntRoundTrip_SetThenGet_ReturnsOriginalValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Int32(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetInt(fieldName.Get, value);
                var retrieved = collection.GetInt(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved == value;
                
                return valuesMatch.ToProperty()
                    .Label($"Int round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any long value set via SetLong, getting it back via GetLong SHALL return
    /// the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LongRoundTrip_SetThenGet_ReturnsOriginalValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Int64(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetLong(fieldName.Get, value);
                var retrieved = collection.GetLong(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved == value;
                
                return valuesMatch.ToProperty()
                    .Label($"Long round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any boolean value set via SetBool, getting it back via GetBool SHALL return
    /// the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BoolRoundTrip_SetThenGet_ReturnsOriginalValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Bool(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetBool(fieldName.Get, value);
                var retrieved = collection.GetBool(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved == value;
                
                return valuesMatch.ToProperty()
                    .Label($"Bool round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }


    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any decimal value set via SetDecimal, getting it back via GetDecimal SHALL return
    /// the same value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DecimalRoundTrip_SetThenGet_ReturnsOriginalValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Decimal(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetDecimal(fieldName.Get, value);
                var retrieved = collection.GetDecimal(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved == value;
                
                return valuesMatch.ToProperty()
                    .Label($"Decimal round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any DateTime value set via SetDateTime, getting it back via GetDateTime SHALL return
    /// an equivalent value (within reasonable precision).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.DateTime(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                // Normalize to UTC for consistent round-trip
                var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
                
                // Act
                collection.SetDateTime(fieldName.Get, utcValue);
                var retrieved = collection.GetDateTime(fieldName.Get);
                
                // Assert - compare with tolerance for ISO 8601 precision
                var valuesMatch = retrieved.HasValue && 
                    Math.Abs((retrieved.Value - utcValue).TotalMilliseconds) < 1;
                
                return valuesMatch.ToProperty()
                    .Label($"DateTime round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any DateTimeOffset value set via SetDateTimeOffset, getting it back via GetDateTimeOffset 
    /// SHALL return an equivalent value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffsetRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.DateTimeOffset(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetDateTimeOffset(fieldName.Get, value);
                var retrieved = collection.GetDateTimeOffset(fieldName.Get);
                
                // Assert - compare with tolerance for ISO 8601 precision
                var valuesMatch = retrieved.HasValue && 
                    Math.Abs((retrieved.Value - value).TotalMilliseconds) < 1;
                
                return valuesMatch.ToProperty()
                    .Label($"DateTimeOffset round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any byte array set via SetBytes, getting it back via GetBytes SHALL return
    /// an equivalent array.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BytesRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Array<byte>(),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                
                // Act
                collection.SetBytes(fieldName.Get, value);
                var retrieved = collection.GetBytes(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved != null && retrieved.SequenceEqual(value);
                
                return valuesMatch.ToProperty()
                    .Label($"Bytes round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any string list set via SetStringList, getting it back via GetStringList SHALL return
    /// an equivalent list.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringListRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.List<NonEmptyString>().Filter(l => l.Count > 0),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                var stringList = value.Select(s => s.Get).ToList();
                
                // Act
                collection.SetStringList(fieldName.Get, stringList);
                var retrieved = collection.GetStringList(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved != null && retrieved.SequenceEqual(stringList);
                
                return valuesMatch.ToProperty()
                    .Label($"StringList round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any string set via SetStringSet, getting it back via GetStringSet SHALL return
    /// an equivalent set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringSetRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Set<NonEmptyString>().Filter(s => s.Count > 0),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                var stringSet = new HashSet<string>(value.Select(s => s.Get));
                
                // Act
                collection.SetStringSet(fieldName.Get, stringSet);
                var retrieved = collection.GetStringSet(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved != null && retrieved.SetEquals(stringSet);
                
                return valuesMatch.ToProperty()
                    .Label($"StringSet round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 15: Serialization Round-Trip**
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// 
    /// For any number set via SetNumberSet, getting it back via GetNumberSet SHALL return
    /// an equivalent set.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NumberSetRoundTrip_SetThenGet_ReturnsEquivalentValue()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.Set<int>().Filter(s => s.Count > 0),
            (fieldName, value) =>
            {
                // Arrange
                var collection = new DynamicFieldCollection();
                var numberSet = new HashSet<int>(value);
                
                // Act
                collection.SetNumberSet(fieldName.Get, numberSet);
                var retrieved = collection.GetNumberSet(fieldName.Get);
                
                // Assert
                var valuesMatch = retrieved != null && retrieved.SetEquals(numberSet);
                
                return valuesMatch.ToProperty()
                    .Label($"NumberSet round-trip should preserve value. ValuesMatch: {valuesMatch}");
            });
    }

    #region Helper Methods

    /// <summary>
    /// Generates a random DynamicFieldCollection with various field types.
    /// </summary>
    private static Arbitrary<DynamicFieldCollection> GenerateDynamicFieldCollection()
    {
        return Arb.From(
            from fieldCount in Gen.Choose(0, 10)
            from fields in Gen.ListOf(fieldCount, GenerateRandomField())
            select new DynamicFieldCollection(
                fields.GroupBy(f => f.Key)
                      .ToDictionary(g => g.Key, g => g.First().Value)));
    }

    /// <summary>
    /// Generates a random field (key-value pair) with a random DynamoDB type.
    /// </summary>
    private static Gen<KeyValuePair<string, AttributeValue>> GenerateRandomField()
    {
        return from fieldName in Arb.Default.NonEmptyString().Generator
               from fieldType in Gen.Choose(0, 5)
               from av in GenerateAttributeValue(fieldType)
               select new KeyValuePair<string, AttributeValue>(fieldName.Get, av);
    }

    /// <summary>
    /// Generates an AttributeValue of the specified type.
    /// </summary>
    private static Gen<AttributeValue> GenerateAttributeValue(int typeIndex)
    {
        return typeIndex switch
        {
            0 => from s in Arb.Default.NonEmptyString().Generator
                 select new AttributeValue { S = s.Get },
            1 => from n in Arb.Default.Int32().Generator
                 select new AttributeValue { N = n.ToString() },
            2 => from b in Arb.Default.Bool().Generator
                 select new AttributeValue { BOOL = b },
            3 => from d in Arb.Default.Decimal().Generator
                 select new AttributeValue { N = d.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            4 => from l in Arb.Default.Int64().Generator
                 select new AttributeValue { N = l.ToString() },
            _ => from s in Arb.Default.NonEmptyString().Generator
                 select new AttributeValue { S = s.Get }
        };
    }

    /// <summary>
    /// Compares two AttributeValues for equivalence.
    /// </summary>
    private static bool AreAttributeValuesEquivalent(AttributeValue a, AttributeValue b)
    {
        if (a.S != null && b.S != null) return a.S == b.S;
        if (a.N != null && b.N != null) return a.N == b.N;
        if (a.IsBOOLSet == true && b.IsBOOLSet == true) return a.BOOL == b.BOOL;
        if (a.NULL == true && b.NULL == true) return true;
        if (a.B != null && b.B != null) return a.B.ToArray().SequenceEqual(b.B.ToArray());
        if (a.SS?.Count > 0 && b.SS?.Count > 0) return a.SS.SequenceEqual(b.SS);
        if (a.NS?.Count > 0 && b.NS?.Count > 0) return a.NS.SequenceEqual(b.NS);
        return false;
    }

    #endregion
}
