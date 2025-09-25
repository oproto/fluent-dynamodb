using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactUpdateBuilder : 
    IWithKey<TransactUpdateBuilder>, IWithConditionExpression<TransactUpdateBuilder>, IWithAttributeNames<TransactUpdateBuilder>, IWithAttributeValues<TransactUpdateBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public TransactUpdateBuilder(string tableName)
    {
        _req.Update = new Update();
        _req.Update.TableName = tableName;
    }

    
    public TransactUpdateBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Update.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Update.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactUpdateBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        _req.Update.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactUpdateBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        _req.Update.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Update.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactUpdateBuilder Where(string conditionExpression)
    {
        _req.Update.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactUpdateBuilder Set(string updateExpression)
    {
        _req.Update.UpdateExpression = updateExpression;
        return this;
    }

    
    public TransactUpdateBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public TransactUpdateBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public TransactUpdateBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public TransactUpdateBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public TransactUpdateBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public TransactUpdateBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactUpdateBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactUpdateBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);

        return this;
    }
    
    public TransactUpdateBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactUpdateBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Update.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        _req.Update.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.Update.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}