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
    private readonly KeysAndAttributes _keysAndAttributes = new KeysAndAttributes() { ConsistentRead = false };
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
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        var key = new Dictionary<string, AttributeValue>();
        keyAction(key);
        _keysAndAttributes.Keys.Add(key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public BatchGetItemBuilder Self => this;



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