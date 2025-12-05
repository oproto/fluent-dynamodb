using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates UpdateItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method updates an order's status and modified date.
/// </summary>
public static class UpdateSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit expression strings and AttributeValue dictionaries.
    /// Uses ReturnValues to get the updated item for equivalency demonstration.
    /// </summary>
    public static async Task<Order?> RawSdkUpdateAsync(
        IAmazonDynamoDB client, string orderId, string newStatus, DateTime modifiedAt)
    {
        var request = new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["sk"] = new AttributeValue { S = "META" }
            },
            UpdateExpression = "SET #status = :status, #modified = :modified",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "orderStatus",
                ["#modified"] = "modifiedAt"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = newStatus },
                [":modified"] = new AttributeValue { S = modifiedAt.ToString("o") }
            },
            ReturnValues = ReturnValue.ALL_NEW
        };

        var response = await client.UpdateItemAsync(request);

        if (response.Attributes == null || response.Attributes.Count == 0)
            return null;

        // Manual conversion of updated item to domain model
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
    /// FluentDynamoDb manual builder - uses WithAttribute() and WithValue() methods.
    /// </summary>
    public static async Task FluentManualUpdateAsync(
        OrdersTable table, string orderId, string newStatus, DateTime modifiedAt)
    {
        await table.Update<Order>()
            .WithKey("pk", $"ORDER#{orderId}", "sk", "META")
            .Set("SET #status = :status, #modified = :modified")
            .WithAttribute("#status", "orderStatus")
            .WithAttribute("#modified", "modifiedAt")
            .WithValue(":status", newStatus)
            .WithValue(":modified", modifiedAt.ToString("o"))
            .UpdateAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - demonstrates {0:o} ISO 8601 date formatting.
    /// </summary>
    public static async Task FluentFormattedUpdateAsync(
        OrdersTable table, string orderId, string newStatus, DateTime modifiedAt)
    {
        await table.Update<Order>()
            .WithKey("pk", Order.Keys.Pk(orderId), "sk", "META")
            .Set("SET #status = {0}, #modified = {1:o}", newStatus, modifiedAt)
            .WithAttribute("#status", "orderStatus")
            .WithAttribute("#modified", "modifiedAt")
            .UpdateAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with strongly-typed Set().
    /// </summary>
    public static async Task FluentLambdaUpdateAsync(
        OrdersTable table, string orderId, string newStatus, DateTime modifiedAt)
    {
        await table.Orders.Update(Order.Keys.Pk(orderId), "META")
            .Set(x => new OrderUpdateModel
            {
                Status = newStatus,
                ModifiedAt = modifiedAt
            })
            .UpdateAsync();
    }
}
