using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactUpdateBuilder : 
    IWithKey<TransactUpdateBuilder>, IWithConditionExpression<TransactUpdateBuilder>, IWithAttributeNames<TransactUpdateBuilder>, IWithAttributeValues<TransactUpdateBuilder>, IWithUpdateExpression<TransactUpdateBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public TransactUpdateBuilder(string tableName)
    {
        _req.Update = new Update();
        _req.Update.TableName = tableName;
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
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetConditionExpression(string expression)
    {
        _req.Update.ConditionExpression = expression;
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        keyAction(_req.Update.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactUpdateBuilder Self => this;

    

    

    
    /// <summary>
    /// Sets the update expression on the builder.
    /// </summary>
    /// <param name="expression">The processed update expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetUpdateExpression(string expression)
    {
        _req.Update.UpdateExpression = expression;
        return this;
    }

    



    
    public TransactUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Update.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        _req.Update.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.Update.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}