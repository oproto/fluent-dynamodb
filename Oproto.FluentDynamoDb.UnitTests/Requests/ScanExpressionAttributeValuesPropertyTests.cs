using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based preservation tests for ScanRequestBuilder.ToScanRequest() and ExpressionAttributeValues
/// (GitHub issue #229). These cover inputs outside the bug condition: scans that hold at least one value,
/// and pre-built requests that carry a non-empty ExpressionAttributeValues with no builder values.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
[Trait("Feature", "scan-empty-expression-attribute-values-fix")]
public class ScanExpressionAttributeValuesPropertyTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty }
            };
        }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
            where TSelf : IReadOnlyEntity
        {
            var entity = new TestEntity { Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }

        public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    /// <summary>
    /// **Feature: scan-empty-expression-attribute-values-fix, Property 2: Preservation**
    /// **Validates: Requirements 3.1**
    ///
    /// For any N > 0 distinct values added via WithValue, ToScanRequest() returns
    /// an ExpressionAttributeValues dictionary with exactly N entries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithValue_NDistinctValues_YieldsExactlyNEntries()
    {
        var valuesArb = Arb.From(
            from count in Gen.Choose(1, 10)
            from values in Gen.ListOf(count, Arb.Default.NonEmptyString().Generator)
            select values.Select(v => v.Get).ToList());

        return Prop.ForAll(valuesArb, values =>
        {
            // Arrange
            var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");

            // Act - distinct placeholder names, so N values produce N entries
            for (var i = 0; i < values.Count; i++)
            {
                builder.WithValue($":v{i}", values[i]);
            }
            var req = builder.ToScanRequest();

            // Assert
            var count = req.ExpressionAttributeValues?.Count ?? -1;
            var allKeysPresent = req.ExpressionAttributeValues != null &&
                Enumerable.Range(0, values.Count).All(i =>
                    req.ExpressionAttributeValues.TryGetValue($":v{i}", out var av) && av.S == values[i]);

            return (count == values.Count && allKeysPresent)
                .ToProperty()
                .Label($"Expected {values.Count} entries, got {count}. AllKeysPresent: {allKeysPresent}");
        });
    }

    /// <summary>
    /// **Feature: scan-empty-expression-attribute-values-fix, Property 2: Preservation**
    /// **Validates: Requirements 3.2**
    ///
    /// For any ScanRequest with a non-empty ExpressionAttributeValues passed to WithRequest with no builder
    /// values, ToScanRequest() returns the same dictionary reference with the same entries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithRequest_NonEmptyExpressionAttributeValues_NoBuilderValues_PreservesSameDictionary()
    {
        return Prop.ForAll(GenerateScanRequestWithValues(), request =>
        {
            // Arrange - capture the original reference and entries before ToScanRequest() runs
            var originalDictionary = request.ExpressionAttributeValues;
            var originalEntries = originalDictionary.ToDictionary(kv => kv.Key, kv => kv.Value);
            var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

            // Act
            builder.WithRequest(request);
            var result = builder.ToScanRequest();

            // Assert
            var sameReference = ReferenceEquals(result.ExpressionAttributeValues, originalDictionary);
            var sameEntries = result.ExpressionAttributeValues != null &&
                result.ExpressionAttributeValues.Count == originalEntries.Count &&
                originalEntries.All(kv =>
                    result.ExpressionAttributeValues.TryGetValue(kv.Key, out var av) && ReferenceEquals(av, kv.Value));

            return (sameReference && sameEntries)
                .ToProperty()
                .Label($"Non-empty ExpressionAttributeValues should be preserved. SameReference: {sameReference}, SameEntries: {sameEntries}");
        });
    }

    /// <summary>
    /// **Feature: scan-empty-expression-attribute-values-fix, Property 1: Bug Condition (N == 0)**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// With zero values added via WithValue (no values at all, or only skipped conditional values),
    /// ToScanRequest() returns a null ExpressionAttributeValues.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithValue_ZeroValuesAdded_YieldsNullExpressionAttributeValues()
    {
        // Number of WithValue calls that are skipped (conditionalUse: false) - none add a value
        return Prop.ForAll(Gen.Choose(0, 10).ToArbitrary(), skippedCalls =>
        {
            // Arrange
            var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
            builder.ForTable("TestTable");

            // Act
            for (var i = 0; i < skippedCalls; i++)
            {
                builder.WithValue($":v{i}", $"value{i}", conditionalUse: false);
            }
            var req = builder.ToScanRequest();

            // Assert
            return (req.ExpressionAttributeValues == null)
                .ToProperty()
                .Label($"Expected null ExpressionAttributeValues with 0 values added ({skippedCalls} skipped calls), got {req.ExpressionAttributeValues?.Count}");
        });
    }

    /// <summary>
    /// **Feature: scan-empty-expression-attribute-values-fix, Property 1: Bug Condition (N == 0)**
    /// **Validates: Requirements 2.4**
    ///
    /// For any pre-built ScanRequest whose ExpressionAttributeValues is null or an empty dictionary,
    /// passed to WithRequest with no builder values, ToScanRequest() returns a null ExpressionAttributeValues.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithRequest_NullOrEmptyExpressionAttributeValues_NoBuilderValues_YieldsNull()
    {
        var requestArb = Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from limit in Gen.Choose(1, 100)
            from useEmpty in Arb.Default.Bool().Generator
            select new ScanRequest
            {
                TableName = tableName.Get,
                Limit = limit,
                ExpressionAttributeValues = useEmpty ? new Dictionary<string, AttributeValue>() : null
            });

        return Prop.ForAll(requestArb, request =>
        {
            // Arrange
            var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

            // Act
            builder.WithRequest(request);
            var result = builder.ToScanRequest();

            // Assert
            return (result.ExpressionAttributeValues == null)
                .ToProperty()
                .Label($"Expected null ExpressionAttributeValues, got {result.ExpressionAttributeValues?.Count}");
        });
    }

    /// <summary>
    /// Local generator: ScanRequest whose ExpressionAttributeValues is always a non-empty dictionary.
    /// (GenerateScanRequest in DirectSdkRequestPropertyTests is private, so it is not reused.)
    /// </summary>
    private static Arbitrary<ScanRequest> GenerateScanRequestWithValues()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from limit in Gen.Choose(1, 100)
            from segment in Gen.Choose(0, 3)
            from totalSegments in Gen.Choose(1, 4)
            from valueCount in Gen.Choose(1, 8)
            from values in Gen.ListOf(valueCount, Arb.Default.NonEmptyString().Generator)
            let dictionary = values
                .Select((v, i) => (Key: $":v{i}", Value: new AttributeValue { S = v.Get }))
                .ToDictionary(x => x.Key, x => x.Value)
            select new ScanRequest
            {
                TableName = tableName.Get,
                Limit = limit,
                Segment = segment,
                TotalSegments = totalSegments,
                ExpressionAttributeValues = dictionary
            });
    }
}
