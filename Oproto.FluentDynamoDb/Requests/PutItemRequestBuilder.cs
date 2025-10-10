using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB PutItem operations.
/// PutItem creates a new item or completely replaces an existing item with the same primary key.
/// Use conditional expressions to prevent overwriting existing items when needed.
/// </summary>
/// <example>
/// <code>
/// // Put a new item
/// var response = await table.Put
///     .WithItem(new Dictionary&lt;string, AttributeValue&gt;
///     {
///         ["id"] = new AttributeValue { S = "123" },
///         ["name"] = new AttributeValue { S = "John Doe" },
///         ["email"] = new AttributeValue { S = "john@example.com" }
///     })
///     .ExecuteAsync();
/// 
/// // Conditional put (only if item doesn't exist)
/// var response = await table.Put
///     .WithItem(item)
///     .Where("attribute_not_exists(id)")
///     .ExecuteAsync();
/// </code>
/// </example>
public class PutItemRequestBuilder : IWithAttributeNames<PutItemRequestBuilder>, IWithAttributeValues<PutItemRequestBuilder>,
    IWithConditionExpression<PutItemRequestBuilder>
{
    public PutItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private PutItemRequest _req = new PutItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Sets the condition expression on the builder.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder SetConditionExpression(string expression)
    {
        _req.ConditionExpression = expression;
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public PutItemRequestBuilder Self => this;
    
    public PutItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }
    






    
    public PutItemRequestBuilder ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }
    
    public PutItemRequestBuilder ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }
    
    public PutItemRequestBuilder ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }
    
    public PutItemRequestBuilder ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }
    
    public PutItemRequestBuilder ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }
    
    public PutItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public PutItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public PutItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public PutItemRequestBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public PutItemRequestBuilder WithItem(Dictionary<string, AttributeValue> item)
    {
        _req.Item = item;
        return this;
    }

    public PutItemRequestBuilder WithItem<TItemType>(TItemType item, Func<TItemType,Dictionary<string, AttributeValue>> modelMapper)
    {
        _req.Item = modelMapper(item);
        return this;
    }
    
    public PutItemRequest ToPutItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    public async Task<PutItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.PutItemAsync(ToPutItemRequest(), cancellationToken);
    }
}