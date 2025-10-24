using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactPutBuilder : IWithConditionExpression<TransactPutBuilder>, IWithAttributeNames<TransactPutBuilder>, IWithAttributeValues<TransactPutBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    public TransactPutBuilder(string tableName)
    {
        _req.Put = new Put();
        _req.Put.TableName = tableName;
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
    public TransactPutBuilder SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.Put.ConditionExpression))
        {
            _req.Put.ConditionExpression = expression;
        }
        else
        {
            _req.Put.ConditionExpression = $"({_req.Put.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactPutBuilder Self => this;







    public TransactPutBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Put.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactPutBuilder WithItem(Dictionary<string, AttributeValue> item)
    {
        _req.Put.Item = item;
        return this;
    }

    public TransactPutBuilder WithItem<TItemType>(TItemType item, Func<TItemType, Dictionary<string, AttributeValue>> modelMapper)
    {
        _req.Put.Item = modelMapper(item);
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        _req.Put.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.Put.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}