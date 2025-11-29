using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB condition check operations in transactions.
/// Condition checks verify conditions without modifying data.
/// This builder is designed for use within transactions and does not support standalone execution.
/// </summary>
/// <typeparam name="TEntity">The entity type being checked.</typeparam>
/// <example>
/// <code>
/// // Use in a transaction
/// await DynamoDbTransactions.Write
///     .Add(table.ConditionCheck&lt;MyEntity&gt;()
///         .WithKey("id", "123")
///         .Where("attribute_exists(#status)")
///         .WithAttribute("#status", "status"))
///     .Add(table.Update(pk, sk).Set(...))
///     .ExecuteAsync();
/// </code>
/// </example>
public class ConditionCheckBuilder<TEntity> :
    IWithKey<ConditionCheckBuilder<TEntity>>,
    IWithConditionExpression<ConditionCheckBuilder<TEntity>>,
    ITransactableConditionCheckBuilder,
    IHasDynamoDbClient
    where TEntity : class
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private string _tableName;
    private Dictionary<string, AttributeValue> _key = new();
    private string? _conditionExpression;
    private readonly AttributeNameInternal _attrN = new();
    private readonly AttributeValueInternal _attrV = new();

    /// <summary>
    /// Initializes a new instance of the ConditionCheckBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for the operation.</param>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    public ConditionCheckBuilder(IAmazonDynamoDB dynamoDbClient, string tableName)
    {
        _dynamoDbClient = dynamoDbClient;
        _tableName = tableName;
    }

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;

    /// <summary>
    /// Gets the DynamoDB client for extension method access.
    /// This is used internally by transaction builders to extract the client.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance used by this builder.</returns>
    public IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConditionCheckBuilder<TEntity> SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_conditionExpression))
        {
            _conditionExpression = expression;
        }
        else
        {
            _conditionExpression = $"({_conditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConditionCheckBuilder<TEntity> SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        keyAction(_key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public ConditionCheckBuilder<TEntity> Self => this;

    /// <summary>
    /// Specifies the table name for the condition check operation.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ConditionCheckBuilder<TEntity> ForTable(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    // ITransactableConditionCheckBuilder implementation
    string ITransactableConditionCheckBuilder.GetTableName() => _tableName;
    
    Dictionary<string, AttributeValue> ITransactableConditionCheckBuilder.GetKey() => _key;
    
    string ITransactableConditionCheckBuilder.GetConditionExpression() => 
        _conditionExpression ?? throw new InvalidOperationException(
            "Condition expression is required for condition checks. Use Where() to specify a condition.");
    
    Dictionary<string, string>? ITransactableConditionCheckBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    
    Dictionary<string, AttributeValue>? ITransactableConditionCheckBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;
}
