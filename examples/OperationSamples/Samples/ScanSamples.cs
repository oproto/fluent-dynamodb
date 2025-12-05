using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates Scan operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method scans for orders with a specific status filter.
/// </summary>
public static class ScanSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit FilterExpression and AttributeValue dictionaries.
    /// Manually converts response items to domain models for equivalency.
    /// </summary>
    public static async Task<List<Order>> RawSdkScanAsync(IAmazonDynamoDB client, string status)
    {
        var request = new ScanRequest
        {
            TableName = TableName,
            FilterExpression = "#status = :status AND sk = :sk",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "orderStatus"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = status },
                [":sk"] = new AttributeValue { S = "META" }
            }
        };

        var response = await client.ScanAsync(request);

        // Manual conversion of items to domain models
        return response.Items.Select(item => new Order
        {
            Pk = item["pk"].S,
            Sk = item["sk"].S,
            OrderId = item["orderId"].S,
            CustomerId = item["customerId"].S,
            OrderDate = DateTime.Parse(item["orderDate"].S),
            Status = item["orderStatus"].S,
            TotalAmount = decimal.Parse(item["totalAmount"].N)
        }).ToList();
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses WithFilter() with WithAttribute() and WithValue() methods.
    /// </summary>
    public static async Task<List<Order>> FluentManualScanAsync(OrdersTable table, string status)
    {
        return await table.Scan()
            .WithFilter("#status = :status AND sk = :sk")
            .WithAttribute("#status", "orderStatus")
            .WithValue(":status", status)
            .WithValue(":sk", "META")
            .ToListAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses positional placeholders for values.
    /// </summary>
    public static async Task<List<Order>> FluentFormattedScanAsync(OrdersTable table, string status)
    {
        return await table.Scan()
            .WithFilter("#status = {0} AND sk = {1}", status, "META")
            .WithAttribute("#status", "orderStatus")
            .ToListAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses strongly-typed filter expressions.
    /// </summary>
    public static async Task<List<Order>> FluentLambdaScanAsync(OrdersTable table, string status)
    {
        return await table.Scan()
            .WithFilter(x => x.Status == status && x.Sk == "META")
            .ToListAsync();
    }
}
