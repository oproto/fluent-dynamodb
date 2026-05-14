using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Preservation property tests for the encryption pipeline fix.
/// These tests capture baseline behavior of all non-bug-condition entity types
/// on UNFIXED code to prevent regressions.
///
/// These tests MUST PASS on unfixed code — they encode current correct behavior.
///
/// **Validates: Requirements 3.1, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
[Collection("OperationContext")]
public class EncryptionPipelinePreservationTests
{
    /// <summary>
    /// Preservation 3.4: Non-encrypted entities → WithItem() serializes synchronously
    /// at builder-configuration time via ToDynamoDb. _req.Item is populated immediately.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property NonEncryptedEntity_WithItem_SerializesSynchronously()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana", "Eve");
        var pkGen = Gen.Elements("pk-001", "pk-002", "pk-003", "pk-004");

        return Prop.ForAll(
            pkGen.ToArbitrary(),
            nameGen.ToArbitrary(),
            (pk, name) =>
            {
                var entity = new PlainTestEntity
                {
                    Pk = pk,
                    Name = name
                };

                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new PutItemRequestBuilder<PlainTestEntity>(client);
                builder.ForTable("test-table");

                // Act: WithItem should serialize synchronously for non-encrypted entities
                builder.WithItem(entity);

                // Assert: _req.Item should be populated immediately (sync serialization)
                var request = builder.ToPutItemRequest();
                var itemPopulated = request.Item != null && request.Item.Count > 0;
                var hasPk = request.Item?.ContainsKey("pk") == true
                            && request.Item["pk"].S == pk;
                var hasName = request.Item?.ContainsKey("name") == true
                              && request.Item["name"].S == name;

                return itemPopulated.ToProperty()
                    .Label("WithItem() should populate _req.Item immediately for non-encrypted entities")
                    .And(hasPk.ToProperty()
                        .Label($"Item should contain pk={pk}"))
                    .And(hasName.ToProperty()
                        .Label($"Item should contain name={name}"));
            });
    }

    /// <summary>
    /// Preservation 3.5: Non-encrypted entities without blob → GetItemAsync uses sync
    /// FromDynamoDb without hydrator lookup. The hydrator registry returns null for plain entities.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property NonEncryptedEntity_HydratorRegistry_ReturnsNull()
    {
        var pkGen = Gen.Elements("pk-001", "pk-002", "pk-003");

        return Prop.ForAll(
            pkGen.ToArbitrary(),
            pk =>
            {
                // The hydrator registry should have no hydrator for plain entities
                var registry = new DefaultEntityHydratorRegistry();
                var hydrator = registry.GetHydrator<PlainTestEntity>();

                return (hydrator == null).ToProperty()
                    .Label("Hydrator registry should return null for plain entities (no hydrator generated)");
            });
    }

    /// <summary>
    /// Preservation 3.1: Plain entities use sync FromDynamoDb for deserialization.
    /// Calling FromDynamoDb directly should work without any async path.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property PlainEntity_FromDynamoDb_WorksSynchronously()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var pkGen = Gen.Elements("pk-001", "pk-002", "pk-003");

        return Prop.ForAll(
            pkGen.ToArbitrary(),
            nameGen.ToArbitrary(),
            (pk, name) =>
            {
                // Build a DynamoDB item dictionary
                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["name"] = new AttributeValue { S = name }
                };

                // Act: sync FromDynamoDb should work for plain entities
                var entity = PlainTestEntity.FromDynamoDb<PlainTestEntity>(item);

                var pkMatch = entity.Pk == pk;
                var nameMatch = entity.Name == name;

                return pkMatch.ToProperty()
                    .Label($"FromDynamoDb should deserialize pk correctly: expected={pk}, actual={entity.Pk}")
                    .And(nameMatch.ToProperty()
                        .Label($"FromDynamoDb should deserialize name correctly: expected={name}, actual={entity.Name}"));
            });
    }

    /// <summary>
    /// Preservation 3.1: Plain entities use sync ToDynamoDb for serialization.
    /// Round-trip through ToDynamoDb → FromDynamoDb preserves all field values.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property PlainEntity_ToDynamoDb_RoundTrip_PreservesValues()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie", "Diana");
        var pkGen = Gen.Elements("pk-001", "pk-002", "pk-003");

        return Prop.ForAll(
            pkGen.ToArbitrary(),
            nameGen.ToArbitrary(),
            (pk, name) =>
            {
                var entity = new PlainTestEntity
                {
                    Pk = pk,
                    Name = name
                };

                // Act: sync round-trip
                var item = PlainTestEntity.ToDynamoDb(entity);
                var restored = PlainTestEntity.FromDynamoDb<PlainTestEntity>(item);

                var pkMatch = restored.Pk == pk;
                var nameMatch = restored.Name == name;

                return pkMatch.ToProperty()
                    .Label($"Round-trip should preserve pk: expected={pk}, actual={restored.Pk}")
                    .And(nameMatch.ToProperty()
                        .Label($"Round-trip should preserve name: expected={name}, actual={restored.Name}"));
            });
    }

    /// <summary>
    /// Preservation 3.4: Non-encrypted entity WithItem() does NOT throw.
    /// This confirms the sync path works correctly for plain entities.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 5)]
    public Property NonEncryptedEntity_WithItem_DoesNotThrow()
    {
        var nameGen = Gen.Elements("Alice", "Bob", "Charlie");
        var pkGen = Gen.Elements("pk-001", "pk-002");

        return Prop.ForAll(
            pkGen.ToArbitrary(),
            nameGen.ToArbitrary(),
            (pk, name) =>
            {
                var entity = new PlainTestEntity
                {
                    Pk = pk,
                    Name = name
                };

                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new PutItemRequestBuilder<PlainTestEntity>(client);
                builder.ForTable("test-table");

                Exception? caughtException = null;
                try
                {
                    builder.WithItem(entity);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }

                return (caughtException == null).ToProperty()
                    .Label($"WithItem() should not throw for non-encrypted entities, " +
                           $"but threw {caughtException?.GetType().Name}: {caughtException?.Message}");
            });
    }
}

/// <summary>
/// Plain test entity with no encryption and no blob storage.
/// This represents the non-bug-condition: no encrypted properties, no blob storage.
/// The source generator will generate synchronous ToDynamoDb/FromDynamoDb methods.
/// </summary>
[DynamoDbTable("plain-test")]
public partial class PlainTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
}
