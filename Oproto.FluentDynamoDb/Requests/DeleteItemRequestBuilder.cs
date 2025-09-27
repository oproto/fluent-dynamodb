using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB DeleteItem operations.
/// Provides a type-safe way to construct delete requests with support for conditional deletes,
/// return values, and consumed capacity tracking.
/// </summary>
/// <example>
/// <code>
/// // Simple delete by primary key
/// await table.Delete
///     .WithKey("id", "user123")
///     .ExecuteAsync();
/// 
/// // Conditional delete with return values
/// var response = await table.Delete
///     .WithKey("pk", "USER", "sk", "user123")
///     .Where("attribute_exists(#status)")
///     .WithAttribute("#status", "status")
///     .ReturnAllOldValues()
///     .ExecuteAsync();
/// </code>
/// </example>
public class DeleteItemRequestBuilder : 
    IWithKey<DeleteItemRequestBuilder>, 
    IWithConditionExpression<DeleteItemRequestBuilder>,
    IWithAttributeNames<DeleteItemRequestBuilder>, 
    IWithAttributeValues<DeleteItemRequestBuilder>
{
    /// <summary>
    /// Initializes a new instance of the DeleteItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    public DeleteItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private DeleteItemRequest _req = new();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    /// <summary>
    /// Specifies the table name for the delete operation.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    /// <summary>
    /// Specifies the primary key for the item to delete using AttributeValue objects.
    /// </summary>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute (optional).</param>
    /// <param name="sortKeyValue">The value of the sort key attribute (optional).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName = null, AttributeValue? sortKeyValue = null)
    {
        _req.Key = new() { { primaryKeyName, primaryKeyValue } };
        if (sortKeyName != null && sortKeyValue != null)
        {
            _req.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    /// <summary>
    /// Specifies a single key attribute for the item to delete using a string value.
    /// </summary>
    /// <param name="keyName">The name of the key attribute.</param>
    /// <param name="keyValue">The string value of the key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    /// <summary>
    /// Specifies the composite primary key for the item to delete using string values.
    /// </summary>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The string value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute.</param>
    /// <param name="sortKeyValue">The string value of the sort key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    /// <summary>
    /// Specifies a condition expression that must be satisfied for the delete operation to proceed.
    /// Use this for conditional deletes to ensure data consistency.
    /// </summary>
    /// <param name="conditionExpression">The condition expression using DynamoDB expression syntax.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Where("attribute_exists(#status) AND #version = :expectedVersion")
    /// </code>
    /// </example>
    public DeleteItemRequestBuilder Where(string conditionExpression)
    {
        _req.ConditionExpression = conditionExpression;
        return this;
    }
    
    /// <summary>
    /// Adds multiple attribute name mappings for use in expressions.
    /// </summary>
    /// <param name="attributeNames">Dictionary mapping expression parameter names to actual attribute names.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithAttributes(Dictionary<string, string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    /// <summary>
    /// Adds multiple attribute name mappings using a configuration action.
    /// </summary>
    /// <param name="attributeNameFunc">Action to configure attribute name mappings.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithAttributes(Action<Dictionary<string, string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    /// <summary>
    /// Adds a single attribute name mapping for use in expressions.
    /// </summary>
    /// <param name="parameterName">The parameter name to use in expressions (e.g., "#status").</param>
    /// <param name="attributeName">The actual attribute name in the table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    /// <summary>
    /// Adds multiple attribute value mappings for use in expressions.
    /// </summary>
    /// <param name="attributeValues">Dictionary mapping expression parameter names to AttributeValue objects.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValues(Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    /// <summary>
    /// Adds multiple attribute value mappings using a configuration action.
    /// </summary>
    /// <param name="attributeValueFunc">Action to configure attribute value mappings.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValues(Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    /// <summary>
    /// Adds a string attribute value mapping for use in expressions.
    /// </summary>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":value").</param>
    /// <param name="attributeValue">The string value to map to the parameter.</param>
    /// <param name="conditionalUse">Whether to add the mapping only if the value is not null.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValue(string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    /// <summary>
    /// Adds a boolean attribute value mapping for use in expressions.
    /// </summary>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":active").</param>
    /// <param name="attributeValue">The boolean value to map to the parameter.</param>
    /// <param name="conditionalUse">Whether to add the mapping only if the value is not null.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValue(string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    /// <summary>
    /// Adds a decimal attribute value mapping for use in expressions.
    /// </summary>
    /// <param name="attributeName">The parameter name to use in expressions (e.g., ":price").</param>
    /// <param name="attributeValue">The decimal value to map to the parameter.</param>
    /// <param name="conditionalUse">Whether to add the mapping only if the value is not null.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValue(string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    /// <summary>
    /// Adds a string dictionary attribute value mapping for use in expressions.
    /// </summary>
    /// <param name="attributeName">The parameter name to use in expressions.</param>
    /// <param name="attributeValue">The string dictionary value to map to the parameter.</param>
    /// <param name="conditionalUse">Whether to add the mapping only if the value is not null.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    /// <summary>
    /// Adds an AttributeValue dictionary mapping for use in expressions.
    /// </summary>
    /// <param name="attributeName">The parameter name to use in expressions.</param>
    /// <param name="attributeValue">The AttributeValue dictionary to map to the parameter.</param>
    /// <param name="conditionalUse">Whether to add the mapping only if the value is not null.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return all attributes of the deleted item as they appeared before deletion.
    /// Useful for audit trails or undo functionality.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }
    
    /// <summary>
    /// Configures the delete operation to return no item attributes (default behavior).
    /// This is the most efficient option when you don't need the deleted item's data.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }
    
    /// <summary>
    /// Configures the delete operation to return the total consumed capacity information.
    /// Useful for monitoring and optimizing DynamoDB usage costs.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    /// <summary>
    /// Configures the delete operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return item collection metrics.
    /// Only applicable for tables with local secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return the old item values when a condition check fails.
    /// Useful for debugging conditional delete failures.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }
    
    /// <summary>
    /// Builds and returns the configured DeleteItemRequest.
    /// </summary>
    /// <returns>A configured DeleteItemRequest ready for execution.</returns>
    public DeleteItemRequest ToDeleteItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    /// <summary>
    /// Executes the delete operation asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the delete response.</returns>
    /// <exception cref="ConditionalCheckFailedException">Thrown when a condition expression fails.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table doesn't exist.</exception>
    public async Task<DeleteItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.DeleteItemAsync(this.ToDeleteItemRequest(), cancellationToken);
    }
}