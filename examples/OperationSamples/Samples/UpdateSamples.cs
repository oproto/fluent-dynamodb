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
    /// </summary>
    public static async Task<UpdateItemResponse> RawSdkUpdateAsync(
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
            }
        };

        return await client.UpdateItemAsync(request);
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
            .WithKey("pk", Order.CreatePk(orderId), "sk", Order.CreateSk())
            .Set("SET #status = {0}, #modified = {1:o}", newStatus, modifiedAt)
            .WithAttribute("#status", "orderStatus")
            .WithAttribute("#modified", "modifiedAt")
            .UpdateAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with key parameters.
    /// </summary>
    public static async Task FluentLambdaUpdateAsync(
        OrdersTable table, string orderId, string newStatus, DateTime modifiedAt)
    {
        await table.Orders.Update(Order.CreatePk(orderId), Order.CreateSk())
            .Set("SET #status = {0}, #modified = {1:o}", newStatus, modifiedAt)
            .WithAttribute("#status", "orderStatus")
            .WithAttribute("#modified", "modifiedAt")
            .UpdateAsync();
    }
}
