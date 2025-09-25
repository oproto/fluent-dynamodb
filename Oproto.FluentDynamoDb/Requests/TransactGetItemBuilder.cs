using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactGetItemBuilder : IWithKey<TransactGetItemBuilder>, IWithAttributeNames<TransactGetItemBuilder>
{
    public TransactGetItemBuilder(string tableName)
    {
        _req.Get = new Get();
        _req.Get.TableName = tableName;
    }
    
    private TransactGetItem _req = new TransactGetItem();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public TransactGetItemBuilder ForTable(string tableName)
    {
        _req.Get.TableName = tableName;
        return this;
    }

    public TransactGetItemBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Get.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Get.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactGetItemBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Get.Key == null) _req.Get.Key = new();
        _req.Get.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactGetItemBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Get.Key == null) _req.Get.Key = new();
        _req.Get.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Get.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactGetItemBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public TransactGetItemBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public TransactGetItemBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }
    
    public TransactGetItemBuilder WithProjection(string projectionExpression)
    {
        _req.Get.ProjectionExpression = projectionExpression;
        return this;
    }
    
    public TransactGetItem ToGetItem()
    {
        _req.Get.ExpressionAttributeNames = _attrN.AttributeNames;
        return _req;
    }
    
}