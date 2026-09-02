using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates PutItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method creates or replaces an order in the table.
/// 
/// Auto key mode: When putting entities, set key properties to raw values (e.g., orderId)
/// instead of calling Order.Keys.Pk(orderId). The "ORDER#" prefix is applied automatically
/// during serialization. Get/Delete/Update operations still use Order.Keys.Pk(value).
/// </summary>
public static class PutSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit AttributeValue dictionaries for all fields.
    /// Returns the order that was put (for equivalency with Fluent methods).
    /// </summary>
    public static async Task RawSdkPutAsync(IAmazonDynamoDB client, string orderId, string customerId, DateTime orderDate, string status, decimal totalAmount)
    {
        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                // Raw SDK requires fully-prefixed key values (no auto key mode)
                ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["sk"] = new AttributeValue { S = "META" },
                ["orderId"] = new AttributeValue { S = orderId },
                ["customerId"] = new AttributeValue { S = customerId },
                ["orderDate"] = new AttributeValue { S = orderDate.ToString("o") },
                ["orderStatus"] = new AttributeValue { S = status },
                ["totalAmount"] = new AttributeValue { N = totalAmount.ToString() }
            }
        };

        await client.PutItemAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses WithItem() with entity mapping.
    /// </summary>
    public static async Task FluentManualPutAsync(OrdersTable table, string orderId, string customerId, DateTime orderDate, string status, decimal totalAmount)
    {
        var order = new Order
        {
            Pk = orderId,           // Auto key mode: "ORDER#" prefix is applied automatically during serialization
            Sk = Order.MetaSk,
            OrderId = orderId,
            CustomerId = customerId,
            OrderDate = orderDate,
            Status = status,
            TotalAmount = totalAmount
        };

        await table.Put<Order>()
            .WithItem(order)
            .PutAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - same as manual for Put operations.
    /// </summary>
    public static async Task FluentFormattedPutAsync(OrdersTable table, string orderId, string customerId, DateTime orderDate, string status, decimal totalAmount)
    {
        var order = new Order
        {
            Pk = orderId,           // Auto key mode: "ORDER#" prefix is applied automatically during serialization
            Sk = Order.MetaSk,
            OrderId = orderId,
            CustomerId = customerId,
            OrderDate = orderDate,
            Status = status,
            TotalAmount = totalAmount
        };

        await table.Put<Order>()
            .WithItem(order)
            .PutAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with automatic mapping.
    /// </summary>
    public static async Task FluentLambdaPutAsync(OrdersTable table, string orderId, string customerId, DateTime orderDate, string status, decimal totalAmount)
    {
        var order = new Order
        {
            Pk = orderId,           // Auto key mode: "ORDER#" prefix is applied automatically during serialization
            Sk = Order.MetaSk,
            OrderId = orderId,
            CustomerId = customerId,
            OrderDate = orderDate,
            Status = status,
            TotalAmount = totalAmount
        };

        await table.Orders.PutAsync(order);
    }
}
