using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Property-based tests for record type entity serialization and deserialization.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class RecordTypePropertyTests
{
    /// <summary>
    /// **Feature: v1-rough-edges, Property 3: Record Type Entity Round-Trip**
    /// **Validates: Requirements 2.2, 2.3**
    /// 
    /// For any record type entity with valid property values, serializing to DynamoDB
    /// and deserializing back SHALL produce an equivalent record instance.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordType_RoundTrip_PreservesValues()
    {
        return Prop.ForAll(
            GenerateTestRecordEntityArbitrary(),
            original =>
            {
                // Act - serialize to DynamoDB format and deserialize back
                var dynamoDbItem = TestRecordEntity.ToDynamoDb(original);
                var restored = TestRecordEntity.FromDynamoDb<TestRecordEntity>(dynamoDbItem);
                
                // Assert - all properties should be preserved
                var idEqual = original.Id == restored.Id;
                var sortKeyEqual = original.SortKey == restored.SortKey;
                var nameEqual = original.Name == restored.Name;
                var valueEqual = original.Value == restored.Value;
                // DateTimeOffset comparison with tolerance for serialization precision
                var createdAtEqual = Math.Abs((original.CreatedAt - restored.CreatedAt).TotalMilliseconds) < 1;
                
                var allEqual = idEqual && sortKeyEqual && nameEqual && valueEqual && createdAtEqual;
                
                return allEqual.ToProperty()
                    .Label($"Record round-trip should preserve all values. " +
                           $"IdEqual: {idEqual}, SortKeyEqual: {sortKeyEqual}, NameEqual: {nameEqual}, " +
                           $"ValueEqual: {valueEqual}, CreatedAtEqual: {createdAtEqual}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 3: Record Type Entity Round-Trip**
    /// **Validates: Requirements 2.2, 2.3**
    /// 
    /// For any positional record type entity, serializing to DynamoDB and deserializing
    /// back SHALL produce an equivalent record instance.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositionalRecordType_RoundTrip_PreservesValues()
    {
        return Prop.ForAll(
            GenerateTestPositionalRecordEntityArbitrary(),
            original =>
            {
                // Act - serialize to DynamoDB format and deserialize back
                var dynamoDbItem = TestPositionalRecordEntity.ToDynamoDb(original);
                var restored = TestPositionalRecordEntity.FromDynamoDb<TestPositionalRecordEntity>(dynamoDbItem);
                
                // Assert - all properties should be preserved
                var idEqual = original.Id == restored.Id;
                var sortKeyEqual = original.SortKey == restored.SortKey;
                var nameEqual = original.Name == restored.Name;
                var countEqual = original.Count == restored.Count;
                
                var allEqual = idEqual && sortKeyEqual && nameEqual && countEqual;
                
                return allEqual.ToProperty()
                    .Label($"Positional record round-trip should preserve all values. " +
                           $"IdEqual: {idEqual}, SortKeyEqual: {sortKeyEqual}, NameEqual: {nameEqual}, " +
                           $"CountEqual: {countEqual}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 3: Record Type Entity Round-Trip**
    /// **Validates: Requirements 2.2, 2.3**
    /// 
    /// For any record type entity with nullable and collection properties, serializing
    /// to DynamoDB and deserializing back SHALL produce an equivalent record instance.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InitOnlyRecordType_RoundTrip_PreservesValues()
    {
        return Prop.ForAll(
            GenerateTestInitOnlyRecordEntityArbitrary(),
            original =>
            {
                // Act - serialize to DynamoDB format and deserialize back
                var dynamoDbItem = TestInitOnlyRecordEntity.ToDynamoDb(original);
                var restored = TestInitOnlyRecordEntity.FromDynamoDb<TestInitOnlyRecordEntity>(dynamoDbItem);
                
                // Assert - all properties should be preserved
                var idEqual = original.Id == restored.Id;
                var sortKeyEqual = original.SortKey == restored.SortKey;
                var descriptionEqual = original.Description == restored.Description;
                var isActiveEqual = original.IsActive == restored.IsActive;
                var tagsEqual = original.Tags.SequenceEqual(restored.Tags);
                
                var allEqual = idEqual && sortKeyEqual && descriptionEqual && isActiveEqual && tagsEqual;
                
                return allEqual.ToProperty()
                    .Label($"Init-only record round-trip should preserve all values. " +
                           $"IdEqual: {idEqual}, SortKeyEqual: {sortKeyEqual}, DescriptionEqual: {descriptionEqual}, " +
                           $"IsActiveEqual: {isActiveEqual}, TagsEqual: {tagsEqual}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 3: Record Type Entity Round-Trip**
    /// **Validates: Requirements 2.2, 2.3**
    /// 
    /// For any record type entity, the DynamoDB item should contain all expected attributes.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordType_ToDynamoDb_ContainsAllAttributes()
    {
        return Prop.ForAll(
            GenerateTestRecordEntityArbitrary(),
            original =>
            {
                // Act - serialize to DynamoDB format
                var dynamoDbItem = TestRecordEntity.ToDynamoDb(original);
                
                // Assert - all expected attributes should be present
                var hasPk = dynamoDbItem.ContainsKey("pk");
                var hasSk = dynamoDbItem.ContainsKey("sk");
                var hasName = dynamoDbItem.ContainsKey("name");
                var hasValue = dynamoDbItem.ContainsKey("value");
                var hasCreatedAt = dynamoDbItem.ContainsKey("created_at");
                
                var allPresent = hasPk && hasSk && hasName && hasValue && hasCreatedAt;
                
                return allPresent.ToProperty()
                    .Label($"DynamoDB item should contain all attributes. " +
                           $"HasPk: {hasPk}, HasSk: {hasSk}, HasName: {hasName}, " +
                           $"HasValue: {hasValue}, HasCreatedAt: {hasCreatedAt}");
            });
    }

    #region Generators

    /// <summary>
    /// Generates arbitrary TestRecordEntity instances with valid property values.
    /// </summary>
    private static Arbitrary<TestRecordEntity> GenerateTestRecordEntityArbitrary()
    {
        return Arb.From(
            from id in GenerateNonEmptyString()
            from sortKey in GenerateNonEmptyString()
            from name in GenerateNonEmptyString()
            from value in Gen.Choose(-1000000, 1000000)
            from createdAt in GenerateDateTimeOffset()
            select new TestRecordEntity
            {
                Id = id,
                SortKey = sortKey,
                Name = name,
                Value = value,
                CreatedAt = createdAt
            });
    }

    /// <summary>
    /// Generates arbitrary TestPositionalRecordEntity instances with valid property values.
    /// </summary>
    private static Arbitrary<TestPositionalRecordEntity> GenerateTestPositionalRecordEntityArbitrary()
    {
        return Arb.From(
            from id in GenerateNonEmptyString()
            from sortKey in GenerateNonEmptyString()
            from name in GenerateNonEmptyString()
            from count in Gen.Choose(0, 1000000)
            select new TestPositionalRecordEntity(id, sortKey, name, count));
    }

    /// <summary>
    /// Generates arbitrary TestInitOnlyRecordEntity instances with valid property values.
    /// </summary>
    private static Arbitrary<TestInitOnlyRecordEntity> GenerateTestInitOnlyRecordEntityArbitrary()
    {
        return Arb.From(
            from id in GenerateNonEmptyString()
            from sortKey in GenerateNonEmptyString()
            from description in Gen.OneOf(
                Gen.Constant<string?>(null),
                GenerateNonEmptyString().Select(s => (string?)s))
            from isActive in Arb.Generate<bool>()
            from tagCount in Gen.Choose(0, 5)
            from tags in Gen.ListOf(tagCount, GenerateNonEmptyString())
            select new TestInitOnlyRecordEntity
            {
                Id = id,
                SortKey = sortKey,
                Description = description,
                IsActive = isActive,
                Tags = tags.ToList()
            });
    }

    /// <summary>
    /// Generates non-empty strings suitable for DynamoDB keys and values.
    /// </summary>
    private static Gen<string> GenerateNonEmptyString()
    {
        return Gen.Elements(
            "test", "value", "key", "item", "record", "entity",
            "alpha", "beta", "gamma", "delta", "epsilon",
            "user-123", "order-456", "product-789",
            "abc", "xyz", "foo", "bar", "baz"
        );
    }

    /// <summary>
    /// Generates DateTimeOffset values for testing.
    /// </summary>
    private static Gen<DateTimeOffset> GenerateDateTimeOffset()
    {
        return from year in Gen.Choose(2000, 2100)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28)
               from hour in Gen.Choose(0, 23)
               from minute in Gen.Choose(0, 59)
               from second in Gen.Choose(0, 59)
               select new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
    }

    #endregion
}
