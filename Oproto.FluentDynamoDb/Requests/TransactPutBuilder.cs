using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactPutBuilder : IWithConditionExpression<TransactPutBuilder>, IWithAttributeNames<TransactPutBuilder>, IWithAttributeValues<TransactPutBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
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
    
    public TransactPutBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public TransactPutBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public TransactPutBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public TransactPutBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public TransactPutBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public TransactPutBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactPutBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactPutBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }

    public TransactPutBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactPutBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
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
        _req.Put.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.Put.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}