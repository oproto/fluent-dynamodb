using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB DeleteItem operations
/// </summary>
public class DeleteItemRequestBuilder : 
    IWithKey<DeleteItemRequestBuilder>, 
    IWithConditionExpression<DeleteItemRequestBuilder>,
    IWithAttributeNames<DeleteItemRequestBuilder>, 
    IWithAttributeValues<DeleteItemRequestBuilder>
{
    private readonly DeleteItemRequest _req = new();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new();
    private readonly AttributeNameInternal _attrN = new();

    public DeleteItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }

    public DeleteItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    // Placeholder implementations - will be fully implemented in task 2.1
    public DeleteItemRequestBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName = null, AttributeValue? sortKeyValue = null)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithKey(string keyName, string keyValue)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder Where(string conditionExpression)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithAttributes(Dictionary<string, string> attributeNames)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithAttributes(Action<Dictionary<string, string>> attributeNameFunc)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValues(Dictionary<string, AttributeValue> attributeValues)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValues(Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValue(string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValue(string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValue(string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }

    public DeleteItemRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 2.1");
    }
}