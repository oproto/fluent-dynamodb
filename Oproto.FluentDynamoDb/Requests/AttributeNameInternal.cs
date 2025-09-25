namespace Oproto.FluentDynamoDb.Requests;

public class AttributeNameInternal
{
    public Dictionary<string, string> AttributeNames { get; init; } = new Dictionary<string, string>();
    
    public void WithAttributes(Dictionary<string,string> attributeNames)
    {
        foreach (var attr in attributeNames)
        {
            AttributeNames.Add(attr.Key, attr.Value);
        }
    }
    
    public void WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        WithAttributes(attributeNames);
    }

    public void WithAttribute(string parameterName, string attributeName)
    {
        AttributeNames.Add(parameterName,attributeName);
    }
}