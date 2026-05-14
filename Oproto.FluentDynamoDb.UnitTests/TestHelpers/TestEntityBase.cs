using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.TestHelpers;

/// <summary>
/// Base test entity that implements IDynamoDbEntity for use in unit tests.
/// This provides a minimal implementation that satisfies the interface requirements.
/// </summary>
public class TestEntityBase : IDynamoDbEntity
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public int Count { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity => new()
        {
            ["pk"] = new AttributeValue { S = (entity as TestEntityBase)?.Id ?? string.Empty },
            ["name"] = new AttributeValue { S = (entity as TestEntityBase)?.Name ?? string.Empty }
        };

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity => (TSelf)(object)new TestEntityBase
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
        };

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity => FromDynamoDb<TSelf>(items.FirstOrDefault() ?? new Dictionary<string, AttributeValue>(), options);

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item) =>
        item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => item.ContainsKey("pk");

    public static bool RequiresWriteTransaction => false;

    public static EntityMetadata GetEntityMetadata() => new()
    {
        TableName = "test-table",
        Properties = new[]
        {
            new PropertyMetadata { PropertyName = "Id", AttributeName = "pk", IsPartitionKey = true },
            new PropertyMetadata { PropertyName = "Name", AttributeName = "name" },
            new PropertyMetadata { PropertyName = "Status", AttributeName = "status" },
            new PropertyMetadata { PropertyName = "Count", AttributeName = "count" }
        },
        Indexes = Array.Empty<IndexMetadata>(),
        Relationships = Array.Empty<RelationshipMetadata>()
    };
}
