using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// A minimal entity implementation for testing raw dictionary-based operations.
/// This entity is used when testing the low-level API with Dictionary&lt;string, AttributeValue&gt;
/// rather than strongly-typed entities.
/// </summary>
public sealed class RawDictionaryEntity : IDynamoDbEntity
{
    public static string TableName => "test-raw-dictionary";
    public static bool RequiresWriteTransaction => false;

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity => new();

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity => (TSelf)(object)new RawDictionaryEntity();

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity => (TSelf)(object)new RawDictionaryEntity();

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item) =>
        item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;

    public static EntityMetadata GetEntityMetadata() => new()
    {
        TableName = TableName,
        Properties = Array.Empty<PropertyMetadata>(),
        Indexes = Array.Empty<IndexMetadata>(),
        Relationships = Array.Empty<RelationshipMetadata>()
    };
}
