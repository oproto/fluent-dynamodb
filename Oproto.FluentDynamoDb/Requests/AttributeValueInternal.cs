using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

internal class AttributeValueInternal()
{
    public Dictionary<string, AttributeValue> AttributeValues { get; init; } = new Dictionary<string, AttributeValue>();
    
    public void WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        foreach(KeyValuePair<string,AttributeValue> kvp in attributeValues)
        {
            this.AttributeValues.Add(kvp.Key, kvp.Value);
        }
    }
    
    public void WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        foreach(KeyValuePair<string,AttributeValue> kvp in attributeValues)
        {
            this.AttributeValues.Add(kvp.Key, kvp.Value);
        }
    }
    
    public void WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse && attributeValue != null)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }
    }
    
    public void WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName,
                new AttributeValue() { BOOL = attributeValue ?? false, IsBOOLSet = attributeValue != null });
        }
    }
    
    public void WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { N = attributeValue.ToString() });
        }
    }
    
    public void WithValue(
        string attributeName, Dictionary<string,string> attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { M = attributeValue.ToDictionary(x => x.Key, x => new AttributeValue() { S = x.Value }) });
        }
    }
    
    public void WithValue(
        string attributeName, Dictionary<string,AttributeValue> attributeValue, bool conditionalUse = true)
    {
        if (conditionalUse)
        {
            AttributeValues.Add(attributeName, new AttributeValue() { M = attributeValue });
        }
    }
}