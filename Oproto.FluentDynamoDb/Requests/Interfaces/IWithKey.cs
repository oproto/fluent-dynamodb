using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

public interface IWithKey<out TBuilder>
{
    public TBuilder WithKey(
        string primaryKeyName,
        AttributeValue primaryKeyValue,
        string? sortKeyName = null,
        AttributeValue? sortKeyValue = null);

    public TBuilder WithKey(string keyName, string keyValue);
    public TBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue);
}