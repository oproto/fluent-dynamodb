using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates DeleteItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method deletes an order by its composite key (pk + sk).
/// </summary>
public static class DeleteSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit AttributeValue dictionaries for key specification.
    /// </summary>
    public static async Task<DeleteItemResponse> RawSdkDeleteAsync(IAmazonDynamoDB client, string orderId)
    {
        var request = new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["sk"] = new AttributeValue { S = "META" }
            }
        };

        return await client.DeleteItemAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses WithKey() with explicit string values.
    /// </summary>
    public static async Task FluentManualDeleteAsync(OrdersTable table, string orderId)
    {
        await table.Delete<Order>()
            .WithKey("pk", $"ORDER#{orderId}", "sk", "META")
            .DeleteAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses constant for sort key value.
    /// </summary>
    public static async Task FluentFormattedDeleteAsync(OrdersTable table, string orderId)
    {
        await table.Delete<Order>()
            .WithKey("pk", $"ORDER#{orderId}", "sk", Order.MetaSk)
            .DeleteAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with key parameters.
    /// </summary>
    public static async Task FluentLambdaDeleteAsync(OrdersTable table, string orderId)
    {
        await table.Orders.DeleteAsync(Order.CreatePk(orderId), Order.CreateSk());
    }
}
