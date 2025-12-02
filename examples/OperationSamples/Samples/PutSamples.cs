using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates PutItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method creates or replaces an order in the table.
/// </summary>
public static class PutSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit AttributeValue dictionaries for all fields.
    /// Returns the order that was put (for equivalency with Fluent methods).
    /// </summary>
    public static async Task<Order> RawSdkPutAsync(IAmazonDynamoDB client, Order order)
    {
        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = order.Pk },
                ["sk"] = new AttributeValue { S = order.Sk },
                ["orderId"] = new AttributeValue { S = order.OrderId },
                ["customerId"] = new AttributeValue { S = order.CustomerId },
                ["orderDate"] = new AttributeValue { S = order.OrderDate.ToString("o") },
                ["orderStatus"] = new AttributeValue { S = order.Status },
                ["totalAmount"] = new AttributeValue { N = order.TotalAmount.ToString() }
            }
        };

        await client.PutItemAsync(request);
        
        // Return the order for equivalency (Put doesn't return item by default)
        return order;
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses WithItem() with entity mapping.
    /// </summary>
    public static async Task FluentManualPutAsync(OrdersTable table, Order order)
    {
        await table.Put<Order>()
            .WithItem(order)
            .PutAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - same as manual for Put operations.
    /// </summary>
    public static async Task FluentFormattedPutAsync(OrdersTable table, Order order)
    {
        await table.Put<Order>()
            .WithItem(order)
            .PutAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with automatic mapping.
    /// </summary>
    public static async Task FluentLambdaPutAsync(OrdersTable table, Order order)
    {
        await table.Orders.PutAsync(order);
    }
}
