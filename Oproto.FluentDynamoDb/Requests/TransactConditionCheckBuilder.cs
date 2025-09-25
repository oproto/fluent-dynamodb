using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactConditionCheckBuilder: 
    IWithKey<TransactConditionCheckBuilder>, IWithConditionExpression<TransactConditionCheckBuilder>, IWithAttributeNames<TransactConditionCheckBuilder>, IWithAttributeValues<TransactConditionCheckBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public TransactConditionCheckBuilder(string tableName)
    {
        _req.ConditionCheck = new ConditionCheck();
        _req.ConditionCheck.TableName = tableName;
    }

    
    public TransactConditionCheckBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.ConditionCheck.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.ConditionCheck.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactConditionCheckBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.ConditionCheck.Key == null) _req.ConditionCheck.Key = new();
        _req.ConditionCheck.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactConditionCheckBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.ConditionCheck.Key == null) _req.ConditionCheck.Key = new();
        _req.ConditionCheck.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.ConditionCheck.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactConditionCheckBuilder Where(string conditionExpression)
    {
        _req.ConditionCheck.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactConditionCheckBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public TransactConditionCheckBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public TransactConditionCheckBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public TransactConditionCheckBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactConditionCheckBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ConditionCheck.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        _req.ConditionCheck.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ConditionCheck.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}