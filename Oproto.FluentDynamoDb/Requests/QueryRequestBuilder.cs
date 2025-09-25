using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class QueryRequestBuilder :
    IWithAttributeNames<QueryRequestBuilder>, IWithConditionExpression<QueryRequestBuilder>, IWithAttributeValues<QueryRequestBuilder>
{
    public QueryRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private QueryRequest _req = new QueryRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public QueryRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }
    
    public QueryRequestBuilder Take(int limit)
    {
        _req.Limit = limit;
        return this;
    }
    
    public QueryRequestBuilder Count()
    {
        _req.Select = Select.COUNT;
        return this;
    }
    
    public QueryRequestBuilder UsingConsistentRead()
    {
        _req.ConsistentRead = true;
        return this;
    }
    
    public QueryRequestBuilder WithFilter(string filterExpression)
    {
        _req.FilterExpression = filterExpression;
        return this;
    }
    
    public QueryRequestBuilder UsingIndex(string indexName)
    {
        _req.IndexName = indexName;
        return this;
    }
    
    public QueryRequestBuilder WithProjection(string projectionExpression)
    {
        _req.ProjectionExpression = projectionExpression;
        _req.Select = Select.SPECIFIC_ATTRIBUTES;
        return this;
    }
    
    public QueryRequestBuilder StartAt(Dictionary<string,AttributeValue> exclusiveStartKey)
    {
        _req.ExclusiveStartKey = exclusiveStartKey;
        return this;
    }
    
    public QueryRequestBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public QueryRequestBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public QueryRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public QueryRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public QueryRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public QueryRequestBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public QueryRequestBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public QueryRequestBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public QueryRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public QueryRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public QueryRequestBuilder Where(string conditionExpression)
    {
        _req.KeyConditionExpression = conditionExpression;
        return this;
    }
    
    public QueryRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public QueryRequestBuilder ReturnIndexConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.INDEXES;
        return this;
    }
    
    public QueryRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public QueryRequestBuilder OrderAscending()
    {
        _req.ScanIndexForward = true;
        return this;
    }

    public QueryRequestBuilder OrderDescending()
    {
        _req.ScanIndexForward = false;
        return this;
    }

    public QueryRequestBuilder ScanIndexForward(bool ascending = true)
    {
        _req.ScanIndexForward = ascending;
        return this;
    }

    public QueryRequest ToQueryRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    public async Task<QueryResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.QueryAsync(ToQueryRequest(), cancellationToken);
    }
}