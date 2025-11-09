using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactUpdateBuilder :
    IWithKey<TransactUpdateBuilder>, IWithConditionExpression<TransactUpdateBuilder>, IWithAttributeNames<TransactUpdateBuilder>, IWithAttributeValues<TransactUpdateBuilder>, IWithUpdateExpression<TransactUpdateBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private UpdateExpressionSource? _updateExpressionSource;

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
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public TransactUpdateBuilder SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.Update.ConditionExpression))
        {
            _req.Update.ConditionExpression = expression;
        }
        else
        {
            _req.Update.ConditionExpression = $"({_req.Update.ConditionExpression}) AND ({expression})";
        }
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
    /// <param name="source">The source of the update expression (string-based or expression-based).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attempting to mix string-based and expression-based Set() methods.</exception>
    public TransactUpdateBuilder SetUpdateExpression(string expression, UpdateExpressionSource source = UpdateExpressionSource.StringBased)
    {
        // Check if we're mixing different approaches
        if (_updateExpressionSource.HasValue && _updateExpressionSource.Value != source)
        {
            var currentApproach = _updateExpressionSource.Value == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";
            var attemptedApproach = source == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";

            throw new InvalidOperationException(
                $"Cannot mix {currentApproach} and {attemptedApproach} methods in the same TransactUpdateBuilder. " +
                $"The builder already has an update expression set using {currentApproach}. " +
                $"Please use only one approach consistently throughout the builder chain. " +
                $"If you need to combine multiple update operations, use multiple property assignments " +
                $"within a single expression-based Set() call, or combine all operations in a single string-based Set() call.");
        }

        _req.Update.UpdateExpression = expression;
        _updateExpressionSource = source;
        return this;
    }






    public TransactUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Update.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.Update.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.Update.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }
}