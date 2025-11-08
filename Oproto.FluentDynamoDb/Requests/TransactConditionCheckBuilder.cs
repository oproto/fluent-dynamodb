using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactConditionCheckBuilder :
    IWithKey<TransactConditionCheckBuilder>, IWithConditionExpression<TransactConditionCheckBuilder>, IWithAttributeNames<TransactConditionCheckBuilder>, IWithAttributeValues<TransactConditionCheckBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    public TransactConditionCheckBuilder(string tableName)
    {
        _req.ConditionCheck = new ConditionCheck();
        _req.ConditionCheck.TableName = tableName;
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
    public TransactConditionCheckBuilder SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.ConditionCheck.ConditionExpression))
        {
            _req.ConditionCheck.ConditionExpression = expression;
        }
        else
        {
            _req.ConditionCheck.ConditionExpression = $"({_req.ConditionCheck.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactConditionCheckBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.ConditionCheck.Key == null) _req.ConditionCheck.Key = new();
        keyAction(_req.ConditionCheck.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactConditionCheckBuilder Self => this;










    public TransactConditionCheckBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ConditionCheck.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ConditionCheck.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.ConditionCheck.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }
}