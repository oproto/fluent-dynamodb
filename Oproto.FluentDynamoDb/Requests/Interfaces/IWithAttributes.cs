using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

public interface IWithAttributeNames<out TBuilder>
{
    public TBuilder UsingExpressionAttributeNames(Dictionary<string, string> attributeNames);

    public TBuilder UsingExpressionAttributeNames(Action<Dictionary<string, string>> attributeNameFunc);
}

public interface IWithAttributeValues<out TBuilder>
{
    public TBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues);

    public TBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc);
    
    public TBuilder WithValue(
        string attributeName, string? attributeValue);
    
    public TBuilder WithValue(
        string attributeName, bool attributeValue);
}