using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactGetItemBuilder : IWithKey<TransactGetItemBuilder>, IWithAttributeNames<TransactGetItemBuilder>
{
    public TransactGetItemBuilder(string tableName)
    {
        _req.Get = new Get();
        _req.Get.TableName = tableName;
    }

    private TransactGetItem _req = new TransactGetItem();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

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
    public TransactGetItemBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Get.Key == null) _req.Get.Key = new();
        keyAction(_req.Get.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public TransactGetItemBuilder Self => this;

    public TransactGetItemBuilder ForTable(string tableName)
    {
        _req.Get.TableName = tableName;
        return this;
    }





    public TransactGetItemBuilder WithProjection(string projectionExpression)
    {
        _req.Get.ProjectionExpression = projectionExpression;
        return this;
    }

    public TransactGetItem ToGetItem()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.Get.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        return _req;
    }

}