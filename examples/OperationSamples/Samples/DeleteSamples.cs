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
    /// Uses ReturnValues to get the deleted item for equivalency demonstration.
    /// </summary>
    public static async Task<Order?> RawSdkDeleteAsync(IAmazonDynamoDB client, string orderId)
    {
        var request = new DeleteItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["sk"] = new AttributeValue { S = "META" }
            },
            ReturnValues = ReturnValue.ALL_OLD
        };

        var response = await client.DeleteItemAsync(request);

        if (response.Attributes == null || response.Attributes.Count == 0)
            return null;

        // Manual conversion of deleted item to domain model
        return new Order
        {
            Pk = response.Attributes["pk"].S,
            Sk = response.Attributes["sk"].S,
            OrderId = response.Attributes["orderId"].S,
            CustomerId = response.Attributes["customerId"].S,
            OrderDate = DateTime.Parse(response.Attributes["orderDate"].S),
            Status = response.Attributes["orderStatus"].S,
            TotalAmount = decimal.Parse(response.Attributes["totalAmount"].N)
        };
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
