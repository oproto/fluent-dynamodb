using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB Scan operations
/// </summary>
public class ScanRequestBuilder : 
    IWithAttributeNames<ScanRequestBuilder>, 
    IWithAttributeValues<ScanRequestBuilder>
{
    private readonly ScanRequest _req = new();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new();
    private readonly AttributeNameInternal _attrN = new();

    public ScanRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }

    public ScanRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    // Placeholder implementations - will be fully implemented in task 3.1
    public ScanRequestBuilder WithAttributes(Dictionary<string, string> attributeNames)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithAttributes(Action<Dictionary<string, string>> attributeNameFunc)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValues(Dictionary<string, AttributeValue> attributeValues)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValues(Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValue(string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValue(string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValue(string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }

    public ScanRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        throw new NotImplementedException("Will be implemented in task 3.1");
    }
}