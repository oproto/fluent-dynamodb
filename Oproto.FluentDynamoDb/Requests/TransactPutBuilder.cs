using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactPutBuilder : IWithConditionExpression<TransactPutBuilder>, IWithAttributeNames<TransactPutBuilder>, IWithAttributeValues<TransactPutBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    
    public TransactPutBuilder(string tableName)
    {
        _req.Put = new Put();
        _req.Put.TableName = tableName;
    }

    public TransactPutBuilder Where(string conditionExpression)
    {
        _req.Put.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactPutBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.Put.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public TransactPutBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.Put.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public TransactPutBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.Put.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactPutBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.Put.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactPutBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.Put.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.Put.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public TransactPutBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.Put.ExpressionAttributeValues ??= new();
        _req.Put.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
        return this;
    }
    
    public TransactPutBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Put.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactPutBuilder WithItem(Dictionary<string, AttributeValue> item)
    {
        _req.Put.Item = item;
        return this;
    }

    public TransactPutBuilder WithItem<TItemType>(TItemType item, Func<TItemType,Dictionary<string, AttributeValue>> modelMapper)
    {
        _req.Put.Item = modelMapper(item);
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        return _req;
    }
}