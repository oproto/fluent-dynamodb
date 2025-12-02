using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates TransactWriteItems operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method performs atomic Put+Update+Delete operations across Order and OrderLine entities.
/// </summary>
public static class TransactionWriteSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - full verbose implementation showing Put+Update+Delete in one transaction.
    /// This demonstrates the extreme verbosity required without any abstraction layer.
    /// </summary>
    public static async Task<TransactWriteItemsResponse> RawSdkTransactionWriteAsync(
        IAmazonDynamoDB client,
        OrderLine newLine,
        string orderId,
        decimal newTotal,
        string deleteLineId)
    {
        var request = new TransactWriteItemsRequest
        {
            TransactItems = new List<TransactWriteItem>
            {
                // Put: Add a new order line
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = TableName,
                        Item = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = newLine.Pk },
                            ["sk"] = new AttributeValue { S = newLine.Sk },
                            ["lineId"] = new AttributeValue { S = newLine.LineId },
                            ["productId"] = new AttributeValue { S = newLine.ProductId },
                            ["productName"] = new AttributeValue { S = newLine.ProductName },
                            ["quantity"] = new AttributeValue { N = newLine.Quantity.ToString() },
                            ["unitPrice"] = new AttributeValue { N = newLine.UnitPrice.ToString() }
                        },
                        ConditionExpression = "attribute_not_exists(pk)"
                    }
                },
                // Update: Update the order total
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = "META" }
                        },
                        UpdateExpression = "SET #total = :total",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#total"] = "totalAmount"
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":total"] = new AttributeValue { N = newTotal.ToString() }
                        },
                        ConditionExpression = "attribute_exists(pk)"
                    }
                },
                // Delete: Remove an old order line
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = $"LINE#{deleteLineId}" }
                        },
                        ConditionExpression = "attribute_exists(pk)"
                    }
                }
            }
        };

        return await client.TransactWriteItemsAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses explicit WithKey(), WithItem(), and expression strings.
    /// </summary>
    public static async Task FluentManualTransactionWriteAsync(
        OrdersTable table,
        OrderLine newLine,
        string orderId,
        decimal newTotal,
        string deleteLineId)
    {
        await DynamoDbTransactions.Write
            .Add(table.Put<OrderLine>()
                .WithItem(newLine)
                .Where("attribute_not_exists(pk)"))
            .Add(table.Update<Order>()
                .WithKey("pk", $"ORDER#{orderId}", "sk", "META")
                .Set("SET #total = :total")
                .WithAttribute("#total", "totalAmount")
                .WithValue(":total", newTotal)
                .Where("attribute_exists(pk)"))
            .Add(table.Delete<OrderLine>()
                .WithKey("pk", $"ORDER#{orderId}", "sk", $"LINE#{deleteLineId}")
                .Where("attribute_exists(pk)"))
            .ExecuteAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses placeholders and helper methods for keys.
    /// </summary>
    public static async Task FluentFormattedTransactionWriteAsync(
        OrdersTable table,
        OrderLine newLine,
        string orderId,
        decimal newTotal,
        string deleteLineId)
    {
        await DynamoDbTransactions.Write
            .Add(table.Put<OrderLine>()
                .WithItem(newLine)
                .Where("attribute_not_exists(pk)"))
            .Add(table.Update<Order>()
                .WithKey("pk", Order.CreatePk(orderId), "sk", Order.CreateSk())
                .Set("SET #total = {0}", newTotal)
                .WithAttribute("#total", "totalAmount")
                .Where("attribute_exists(pk)"))
            .Add(table.Delete<OrderLine>()
                .WithKey("pk", OrderLine.CreatePk(orderId), "sk", OrderLine.CreateSk(deleteLineId))
                .Where("attribute_exists(pk)"))
            .ExecuteAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessors with strongly-typed Set().
    /// Note: Transaction builders use format strings for Where() conditions.
    /// </summary>
    public static async Task FluentLambdaTransactionWriteAsync(
        OrdersTable table,
        OrderLine newLine,
        string orderId,
        decimal newTotal,
        string deleteLineId)
    {
        await DynamoDbTransactions.Write
            .Add(table.OrderLines.Put(newLine)
                .Where("attribute_not_exists(pk)"))
            .Add(table.Orders.Update(Order.CreatePk(orderId), Order.CreateSk())
                .Set(x => new OrderUpdateModel { TotalAmount = newTotal })
                .Where("attribute_exists(pk)"))
            .Add(table.OrderLines.Delete(OrderLine.CreatePk(orderId), OrderLine.CreateSk(deleteLineId))
                .Where("attribute_exists(pk)"))
            .ExecuteAsync();
    }
}
