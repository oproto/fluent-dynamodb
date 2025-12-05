using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Placeholder entity for example code.
/// In actual implementations, replace this with your real entity types.
/// </summary>
public class PlaceholderEntity : IDynamoDbEntity
{
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return new Dictionary<string, AttributeValue>();
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        throw new NotImplementedException();
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        throw new NotImplementedException();
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return false;
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata();
    }
}
