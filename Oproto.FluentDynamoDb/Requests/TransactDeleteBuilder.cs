using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactDeleteBuilder :
    IWithKey<TransactDeleteBuilder>, IWithConditionExpression<TransactDeleteBuilder>, IWithAttributeNames<TransactDeleteBuilder>, IWithAttributeValues<TransactDeleteBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    public TransactDeleteBuilder(string tableName)
    {
        _req.Delete = new();
        _req.Delete.TableName = tableName;
    }

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
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactDeleteBuilder SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.Delete.ConditionExpression))
        {
            _req.Delete.ConditionExpression = expression;
        }
        else
        {
            _req.Delete.ConditionExpression = $"({_req.Delete.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactDeleteBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Delete.Key == null) _req.Delete.Key = new();
        keyAction(_req.Delete.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactDeleteBuilder Self => this;












    public TransactDeleteBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Delete.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.Delete.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.Delete.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }
}