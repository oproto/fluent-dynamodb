using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Property-based tests for DynamicEntity.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class DynamicEntityPropertyTests
{
    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// For any DynamoDB item with arbitrary attributes, converting to DynamicEntity and back 
    /// to AttributeValue dictionary should produce an equivalent dictionary.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_RoundTrip_PreservesAllAttributes()
    {
        return Prop.ForAll(
            GenerateAttributeValueDictionary(),
            originalItem =>
            {
                // Act - convert to DynamicEntity and back
                var entity = DynamicEntity.FromDynamoDb<DynamicEntity>(originalItem);
                var roundTrippedItem = DynamicEntity.ToDynamoDb(entity);
                
                // Assert - all attributes should be preserved
                var sameCount = originalItem.Count == roundTrippedItem.Count;
                var allKeysPreserved = originalItem.Keys.All(key => roundTrippedItem.ContainsKey(key));
                var allValuesPreserved = originalItem.All(kvp => 
                    roundTrippedItem.ContainsKey(kvp.Key) && 
                    AreAttributeValuesEquivalent(kvp.Value, roundTrippedItem[kvp.Key]));
                
                return (sameCount && allKeysPreserved && allValuesPreserved).ToProperty()
                    .Label($"Round-trip should preserve all attributes. " +
                           $"SameCount: {sameCount} (original: {originalItem.Count}, roundTripped: {roundTrippedItem.Count}), " +
                           $"AllKeysPreserved: {allKeysPreserved}, AllValuesPreserved: {allValuesPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// For any DynamicEntity with fields set via typed setters, converting to DynamoDB and back
    /// should preserve all field values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_TypedSetters_RoundTrip_PreservesValues()
    {
        return Prop.ForAll(
            GenerateTypedFieldsInput(),
            input =>
            {
                // Arrange - create entity with typed fields
                var entity = new DynamicEntity();
                entity.DynamicFields.SetString(input.StringFieldName, input.StringValue);
                entity.DynamicFields.SetInt("intField", input.IntValue);
                entity.DynamicFields.SetBool("boolField", input.BoolValue);
                
                // Act - convert to DynamoDB and back
                var dynamoDbItem = DynamicEntity.ToDynamoDb(entity);
                var roundTrippedEntity = DynamicEntity.FromDynamoDb<DynamicEntity>(dynamoDbItem);
                
                // Assert - all values should be preserved
                var stringPreserved = roundTrippedEntity.DynamicFields.GetString(input.StringFieldName) == input.StringValue;
                var intPreserved = roundTrippedEntity.DynamicFields.GetInt("intField") == input.IntValue;
                var boolPreserved = roundTrippedEntity.DynamicFields.GetBool("boolField") == input.BoolValue;
                
                return (stringPreserved && intPreserved && boolPreserved).ToProperty()
                    .Label($"Typed setters round-trip should preserve values. " +
                           $"StringPreserved: {stringPreserved}, IntPreserved: {intPreserved}, BoolPreserved: {boolPreserved}");
            });
    }
    
    /// <summary>
    /// Input record for typed fields test.
    /// </summary>
    private record TypedFieldsInput(string StringFieldName, string StringValue, int IntValue, bool BoolValue);
    
    /// <summary>
    /// Generates input for typed fields test.
    /// </summary>
    private static Arbitrary<TypedFieldsInput> GenerateTypedFieldsInput()
    {
        return Arb.From(
            from stringFieldName in Arb.Default.NonEmptyString().Generator
            from stringValue in Arb.Default.NonEmptyString().Generator
            from intValue in Arb.Default.Int32().Generator
            from boolValue in Arb.Default.Bool().Generator
            select new TypedFieldsInput(stringFieldName.Get, stringValue.Get, intValue, boolValue));
    }


    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// For any empty DynamoDB item, converting to DynamicEntity and back should produce an empty dictionary.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_EmptyItem_RoundTrip_ProducesEmptyDictionary()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Arrange - empty item
                var emptyItem = new Dictionary<string, AttributeValue>();
                
                // Act - convert to DynamicEntity and back
                var entity = DynamicEntity.FromDynamoDb<DynamicEntity>(emptyItem);
                var roundTrippedItem = DynamicEntity.ToDynamoDb(entity);
                
                // Assert - should be empty
                var isEmpty = roundTrippedItem.Count == 0;
                var entityFieldsEmpty = entity.DynamicFields.Count == 0;
                
                return (isEmpty && entityFieldsEmpty).ToProperty()
                    .Label($"Empty item round-trip should produce empty dictionary. " +
                           $"IsEmpty: {isEmpty}, EntityFieldsEmpty: {entityFieldsEmpty}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// DynamicEntity.GetEntityMetadata() should return metadata with IsDynamicEntity = true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_GetEntityMetadata_ReturnsIsDynamicEntityTrue()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Act
                var metadata = DynamicEntity.GetEntityMetadata();
                
                // Assert
                var isDynamicEntity = metadata.IsDynamicEntity;
                var hasEmptyTableName = string.IsNullOrEmpty(metadata.TableName);
                var hasNoProperties = metadata.Properties.Length == 0;
                
                return (isDynamicEntity && hasEmptyTableName && hasNoProperties).ToProperty()
                    .Label($"GetEntityMetadata should return IsDynamicEntity=true. " +
                           $"IsDynamicEntity: {isDynamicEntity}, HasEmptyTableName: {hasEmptyTableName}, HasNoProperties: {hasNoProperties}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// DynamicEntity.MatchesEntity should always return true for any item.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_MatchesEntity_AlwaysReturnsTrue()
    {
        return Prop.ForAll(
            GenerateAttributeValueDictionary(),
            item =>
            {
                // Act
                var matches = DynamicEntity.MatchesEntity(item);
                
                // Assert
                return matches.ToProperty()
                    .Label($"MatchesEntity should always return true. Matches: {matches}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// DynamicEntity.RequiresWriteTransaction should always return false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_RequiresWriteTransaction_AlwaysReturnsFalse()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Act
                var requiresTransaction = DynamicEntity.RequiresWriteTransaction;
                
                // Assert
                return (!requiresTransaction).ToProperty()
                    .Label($"RequiresWriteTransaction should always return false. RequiresTransaction: {requiresTransaction}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 2: DynamicEntity round-trip consistency**
    /// **Validates: Requirements 5.6, 7.1**
    /// 
    /// After FromDynamoDb, the DynamicFields collection should have change tracking enabled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_FromDynamoDb_EnablesChangeTracking()
    {
        return Prop.ForAll(
            GenerateAttributeValueDictionary(),
            item =>
            {
                // Act - create entity from DynamoDB item
                var entity = DynamicEntity.FromDynamoDb<DynamicEntity>(item);
                
                // Make a modification to trigger change tracking
                entity.DynamicFields.SetString("newField", "newValue");
                
                // Assert - HasChanges should be true after modification
                var hasChanges = entity.DynamicFields.HasChanges;
                
                return hasChanges.ToProperty()
                    .Label($"FromDynamoDb should enable change tracking. HasChanges: {hasChanges}");
            });
    }

    #region Helper Methods

    /// <summary>
    /// Generates a random AttributeValue dictionary with various field types.
    /// </summary>
    private static Arbitrary<Dictionary<string, AttributeValue>> GenerateAttributeValueDictionary()
    {
        return Arb.From(
            from fieldCount in Gen.Choose(0, 10)
            from fields in Gen.ListOf(fieldCount, GenerateRandomField())
            select fields.GroupBy(f => f.Key)
                         .ToDictionary(g => g.Key, g => g.First().Value));
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
