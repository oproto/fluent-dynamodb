using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates TransactGetItems operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method retrieves an order and its line item atomically with snapshot isolation.
/// </summary>
public static class TransactionGetSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - full verbose implementation showing explicit request construction.
    /// This demonstrates the verbosity required for transaction gets without any abstraction.
    /// </summary>
    public static async Task<TransactGetItemsResponse> RawSdkTransactionGetAsync(
        IAmazonDynamoDB client, string orderId, string lineId)
    {
        var request = new TransactGetItemsRequest
        {
            TransactItems = new List<TransactGetItem>
            {
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = "META" }
                        }
                    }
                },
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = $"LINE#{lineId}" }
                        }
                    }
                }
            }
        };

        return await client.TransactGetItemsAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses Get() with explicit key values.
    /// </summary>
    public static async Task<(Order?, OrderLine?)> FluentManualTransactionGetAsync(
        OrdersTable table, string orderId, string lineId)
    {
        return await DynamoDbTransactions.Get
            .Add(table.Get<Order>().WithKey("pk", $"ORDER#{orderId}", "sk", "META"))
            .Add(table.Get<OrderLine>().WithKey("pk", $"ORDER#{orderId}", "sk", $"LINE#{lineId}"))
            .ExecuteAndMapAsync<Order, OrderLine>();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses helper methods for key construction.
    /// </summary>
    public static async Task<(Order?, OrderLine?)> FluentFormattedTransactionGetAsync(
        OrdersTable table, string orderId, string lineId)
    {
        return await DynamoDbTransactions.Get
            .Add(table.Get<Order>().WithKey("pk", Order.CreatePk(orderId), "sk", Order.CreateSk()))
            .Add(table.Get<OrderLine>().WithKey("pk", OrderLine.CreatePk(orderId), "sk", OrderLine.CreateSk(lineId)))
            .ExecuteAndMapAsync<Order, OrderLine>();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessors with strongly-typed keys.
    /// </summary>
    public static async Task<(Order?, OrderLine?)> FluentLambdaTransactionGetAsync(
        OrdersTable table, string orderId, string lineId)
    {
        return await DynamoDbTransactions.Get
            .Add(table.Orders.Get(Order.CreatePk(orderId), Order.CreateSk()))
            .Add(table.OrderLines.Get(OrderLine.CreatePk(orderId), OrderLine.CreateSk(lineId)))
            .ExecuteAndMapAsync<Order, OrderLine>();
    }
}
