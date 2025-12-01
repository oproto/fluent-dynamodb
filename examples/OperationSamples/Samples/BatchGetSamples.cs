using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates BatchGetItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method retrieves an order and multiple line items in a single batch request.
/// </summary>
public static class BatchGetSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - full verbose implementation showing explicit request construction.
    /// This demonstrates the verbosity required for batch gets without any abstraction.
    /// </summary>
    public static async Task<BatchGetItemResponse> RawSdkBatchGetAsync(
        IAmazonDynamoDB client, string orderId, string lineId1, string lineId2)
    {
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [TableName] = new KeysAndAttributes
                {
                    Keys = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = "META" }
                        },
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = $"LINE#{lineId1}" }
                        },
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                            ["sk"] = new AttributeValue { S = $"LINE#{lineId2}" }
                        }
                    }
                }
            }
        };

        return await client.BatchGetItemAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses Get() with explicit key values.
    /// </summary>
    public static async Task<(Order?, OrderLine?, OrderLine?)> FluentManualBatchGetAsync(
        OrdersTable table, string orderId, string lineId1, string lineId2)
    {
        return await DynamoDbBatch.Get
            .Add(table.Get<Order>().WithKey("pk", $"ORDER#{orderId}", "sk", "META"))
            .Add(table.Get<OrderLine>().WithKey("pk", $"ORDER#{orderId}", "sk", $"LINE#{lineId1}"))
            .Add(table.Get<OrderLine>().WithKey("pk", $"ORDER#{orderId}", "sk", $"LINE#{lineId2}"))
            .ExecuteAndMapAsync<Order, OrderLine, OrderLine>();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses helper methods for key construction.
    /// </summary>
    public static async Task<(Order?, OrderLine?, OrderLine?)> FluentFormattedBatchGetAsync(
        OrdersTable table, string orderId, string lineId1, string lineId2)
    {
        return await DynamoDbBatch.Get
            .Add(table.Get<Order>().WithKey("pk", Order.CreatePk(orderId), "sk", Order.CreateSk()))
            .Add(table.Get<OrderLine>().WithKey("pk", OrderLine.CreatePk(orderId), "sk", OrderLine.CreateSk(lineId1)))
            .Add(table.Get<OrderLine>().WithKey("pk", OrderLine.CreatePk(orderId), "sk", OrderLine.CreateSk(lineId2)))
            .ExecuteAndMapAsync<Order, OrderLine, OrderLine>();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessors with strongly-typed keys.
    /// </summary>
    public static async Task<(Order?, OrderLine?, OrderLine?)> FluentLambdaBatchGetAsync(
        OrdersTable table, string orderId, string lineId1, string lineId2)
    {
        return await DynamoDbBatch.Get
            .Add(table.Orders.Get(Order.CreatePk(orderId), Order.CreateSk()))
            .Add(table.OrderLines.Get(OrderLine.CreatePk(orderId), OrderLine.CreateSk(lineId1)))
            .Add(table.OrderLines.Get(OrderLine.CreatePk(orderId), OrderLine.CreateSk(lineId2)))
            .ExecuteAndMapAsync<Order, OrderLine, OrderLine>();
    }
}
