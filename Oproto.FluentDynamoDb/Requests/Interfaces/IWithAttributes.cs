using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

public interface IWithAttributeNames<out TBuilder>
{
    public TBuilder WithAttributes(Dictionary<string, string> attributeNames);

    public TBuilder WithAttributes(Action<Dictionary<string, string>> attributeNameFunc);

    public TBuilder WithAttribute(string parameterName, string attributeName);
}

public interface IWithAttributeValues<out TBuilder>
{
    public TBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues);

    public TBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc);
    
    public TBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true);
    
    public TBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true);
    
    public TBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true);

    public TBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true);
    
    public TBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true);
}