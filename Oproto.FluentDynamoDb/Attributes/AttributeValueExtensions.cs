using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.DynamoDBEvents;

namespace Oproto.FluentDynamoDb.Attributes;

public static class AttributeValueExtensions
{
    public static AttributeValue? ForKey(this IDictionary<string, AttributeValue> values, string key)
    {
        AttributeValue? value = null;
        if (!values.TryGetValue(key, out value)) return null;
        return value;
    }
    
    public static DynamoDBEvent.AttributeValue? ForKey(this IDictionary<string, DynamoDBEvent.AttributeValue> values, string key)
    {
        DynamoDBEvent.AttributeValue? value = null;
        if (!values.TryGetValue(key, out value)) return null;
        return value;
    }
}