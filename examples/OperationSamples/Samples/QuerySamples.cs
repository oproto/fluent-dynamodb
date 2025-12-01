using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates Query operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method queries order lines for a specific order using the partition key.
/// </summary>
public static class QuerySamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - explicit KeyConditionExpression and AttributeValue dictionaries.
    /// </summary>
    public static async Task<QueryResponse> RawSdkQueryAsync(IAmazonDynamoDB client, string orderId)
    {
        var request = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"ORDER#{orderId}" },
                [":skPrefix"] = new AttributeValue { S = "LINE#" }
            }
        };

        return await client.QueryAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses Where() with WithValue() methods.
    /// </summary>
    public static async Task<List<OrderLine>> FluentManualQueryAsync(OrdersTable table, string orderId)
    {
        return await table.Query<OrderLine>()
            .Where("pk = :pk AND begins_with(sk, :skPrefix)")
            .WithValue(":pk", $"ORDER#{orderId}")
            .WithValue(":skPrefix", "LINE#")
            .ToListAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses positional placeholders for values.
    /// </summary>
    public static async Task<List<OrderLine>> FluentFormattedQueryAsync(OrdersTable table, string orderId)
    {
        return await table.Query<OrderLine>()
            .Where("pk = {0} AND begins_with(sk, {1})", OrderLine.CreatePk(orderId), "LINE#")
            .ToListAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessor with strongly-typed expressions.
    /// </summary>
    public static async Task<List<OrderLine>> FluentLambdaQueryAsync(OrdersTable table, string orderId)
    {
        return await table.OrderLines.Query()
            .Where(x => x.Pk == OrderLine.CreatePk(orderId) && x.Sk.StartsWith("LINE#"))
            .ToListAsync();
    }
}
