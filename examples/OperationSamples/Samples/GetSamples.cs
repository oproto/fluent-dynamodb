using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates GetItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method retrieves an order by its composite key (pk + sk).
/// </summary>
public static class GetSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit AttributeValue dictionaries and manual request construction.
    /// </summary>
    public static async Task<GetItemResponse> RawSdkGetAsync(IAmazonDynamoDB client, string orderId)
    {
        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["sk"] = new AttributeValue { S = "META" }
            }
        };

        return await client.GetItemAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses WithKey() with explicit string values.
    /// </summary>
    public static async Task<Order?> FluentManualGetAsync(OrdersTable table, string orderId)
    {
        return await table.Get<Order>()
            .WithKey("pk", $"ORDER#{orderId}", "sk", "META")
            .GetItemAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses constants for key values.
    /// </summary>
    public static async Task<Order?> FluentFormattedGetAsync(OrdersTable table, string orderId)
    {
        return await table.Get<Order>()
            .WithKey("pk", $"ORDER#{orderId}", "sk", Order.MetaSk)
            .GetItemAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses strongly-typed helper methods for keys.
    /// </summary>
    public static async Task<Order?> FluentLambdaGetAsync(OrdersTable table, string orderId)
    {
        return await table.Orders.GetAsync(Order.CreatePk(orderId), Order.CreateSk());
    }
}
