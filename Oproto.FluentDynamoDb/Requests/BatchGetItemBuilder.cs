using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for configuring items to retrieve from a single table in a BatchGetItem operation.
/// This builder is used within BatchGetItemRequestBuilder to specify which items to retrieve
/// from each table, along with projection expressions and read consistency options.
/// </summary>
public class BatchGetItemBuilder : IWithKey<BatchGetItemBuilder>, IWithAttributeNames<BatchGetItemBuilder>
{
    private readonly KeysAndAttributes _keysAndAttributes = new KeysAndAttributes();
    private readonly string _tableName;
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    /// <summary>
    /// Initializes a new instance of the BatchGetItemBuilder for a specific table.
    /// </summary>
    /// <param name="tableName">The name of the table to retrieve items from.</param>
    public BatchGetItemBuilder(string tableName)
    {
        _tableName = tableName;
        _keysAndAttributes.Keys = new List<Dictionary<string, AttributeValue>>();
    }

    /// <summary>
    /// Adds a key for an item to retrieve using AttributeValue objects.
    /// Call this method multiple times to retrieve multiple items from the table.
    /// </summary>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute (optional).</param>
    /// <param name="sortKeyValue">The value of the sort key attribute (optional).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName = null, AttributeValue? sortKeyValue = null)
    {
        var key = new Dictionary<string, AttributeValue> { { primaryKeyName, primaryKeyValue } };
        if (sortKeyName != null && sortKeyValue != null)
        {
            key.Add(sortKeyName, sortKeyValue);
        }
        _keysAndAttributes.Keys.Add(key);
        return this;
    }

    /// <summary>
    /// Adds a single key attribute for an item to retrieve using a string value.
    /// </summary>
    /// <param name="keyName">The name of the key attribute.</param>
    /// <param name="keyValue">The string value of the key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithKey(string keyName, string keyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { keyName, new AttributeValue { S = keyValue } }
        };
        _keysAndAttributes.Keys.Add(key);
        return this;
    }

    /// <summary>
    /// Adds a composite primary key for an item to retrieve using string values.
    /// </summary>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The string value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute.</param>
    /// <param name="sortKeyValue">The string value of the sort key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { primaryKeyName, new AttributeValue { S = primaryKeyValue } },
            { sortKeyName, new AttributeValue { S = sortKeyValue } }
        };
        _keysAndAttributes.Keys.Add(key);
        return this;
    }

    /// <summary>
    /// Specifies which attributes to retrieve from each item.
    /// This reduces network traffic and can improve performance.
    /// </summary>
    /// <param name="projectionExpression">The projection expression specifying which attributes to retrieve.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithProjection("#id, #name, #email")
    /// </code>
    /// </example>
    public BatchGetItemBuilder WithProjection(string projectionExpression)
    {
        _keysAndAttributes.ProjectionExpression = projectionExpression;
        return this;
    }

    /// <summary>
    /// Enables strongly consistent reads for all items retrieved from this table.
    /// Note: Consistent reads consume twice the read capacity and are not supported on global secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder UsingConsistentRead()
    {
        _keysAndAttributes.ConsistentRead = true;
        return this;
    }

    /// <summary>
    /// Adds multiple attribute name mappings for use in projection expressions.
    /// </summary>
    /// <param name="attributeNames">Dictionary mapping expression parameter names to actual attribute names.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithAttributes(Dictionary<string, string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }

    /// <summary>
    /// Adds multiple attribute name mappings using a configuration action.
    /// </summary>
    /// <param name="attributeNameFunc">Action to configure attribute name mappings.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithAttributes(Action<Dictionary<string, string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    /// <summary>
    /// Adds a single attribute name mapping for use in projection expressions.
    /// </summary>
    /// <param name="parameterName">The parameter name to use in expressions (e.g., "#name").</param>
    /// <param name="attributeName">The actual attribute name in the table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    /// <summary>
    /// Builds and returns the configured KeysAndAttributes for this table.
    /// This method is used internally by BatchGetItemRequestBuilder.
    /// </summary>
    /// <returns>A configured KeysAndAttributes object ready for use in a BatchGetItem request.</returns>
    public KeysAndAttributes ToKeysAndAttributes()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _keysAndAttributes.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        return _keysAndAttributes;
    }
}