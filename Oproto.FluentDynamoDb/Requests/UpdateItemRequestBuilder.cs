using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB UpdateItem operations.
/// UpdateItem modifies existing items or creates them if they don't exist (upsert behavior).
/// Use update expressions to specify which attributes to modify and how to modify them.
/// </summary>
/// <example>
/// <code>
/// // Update specific attributes
/// var response = await table.Update
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     .WithAttribute("#name", "name")
///     .WithAttribute("#status", "status")
///     .WithValue(":name", "John Doe")
///     .WithValue(":status", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Conditional update
/// var response = await table.Update
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     .WithAttribute("#count", "count")
///     .WithValue(":inc", 1)
///     .ExecuteAsync();
/// </code>
/// </example>
public class UpdateItemRequestBuilder : 
    IWithKey<UpdateItemRequestBuilder>, IWithConditionExpression<UpdateItemRequestBuilder>, IWithAttributeNames<UpdateItemRequestBuilder>, IWithAttributeValues<UpdateItemRequestBuilder>, IWithUpdateExpression<UpdateItemRequestBuilder>
{
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private UpdateItemRequest _req = new();
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
    public UpdateItemRequestBuilder SetConditionExpression(string expression)
    {
        _req.ConditionExpression = expression;
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Key == null) _req.Key = new();
        keyAction(_req.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public UpdateItemRequestBuilder Self => this;
    
    public UpdateItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }
    

    

    
    /// <summary>
    /// Sets the update expression on the builder.
    /// </summary>
    /// <param name="expression">The processed update expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder SetUpdateExpression(string expression)
    {
        _req.UpdateExpression = expression;
        return this;
    }
    
    




    public UpdateItemRequestBuilder ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public UpdateItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public UpdateItemRequestBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }
    
    public UpdateItemRequest ToUpdateItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    public async Task<UpdateItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.UpdateItemAsync(this.ToUpdateItemRequest(), cancellationToken);
    }
}