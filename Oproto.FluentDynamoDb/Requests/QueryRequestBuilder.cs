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
    
    public QueryRequestBuilder UsingConsistantRead()
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
    
    public QueryRequestBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public QueryRequestBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public QueryRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public QueryRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }


    public QueryRequestBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }

    public QueryRequestBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
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

    public QueryRequest ToQueryRequest()
    {
        return _req;
    }

    public async Task<QueryResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.QueryAsync(ToQueryRequest(), cancellationToken);
    }
}