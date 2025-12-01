using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates BatchWriteItem operations comparing Raw AWS SDK with FluentDynamoDb approaches.
/// Each method performs bulk put and delete operations across Order and OrderLine entities.
/// Note: Batch operations do not support condition expressions.
/// </summary>
public static class BatchWriteSamples
{
    private const string TableName = "Orders";

    /// <summary>
    /// Raw AWS SDK approach - full verbose implementation showing Put+Delete in one batch.
    /// This demonstrates the extreme verbosity required without any abstraction layer.
    /// </summary>
    public static async Task<BatchWriteItemResponse> RawSdkBatchWriteAsync(
        IAmazonDynamoDB client,
        Order newOrder,
        OrderLine newLine1,
        OrderLine newLine2,
        string deleteOrderId,
        string deleteLineId)
    {
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [TableName] = new List<WriteRequest>
                {
                    // Put: Add a new order
                    new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = newOrder.Pk },
                                ["sk"] = new AttributeValue { S = newOrder.Sk },
                                ["orderId"] = new AttributeValue { S = newOrder.OrderId },
                                ["customerId"] = new AttributeValue { S = newOrder.CustomerId },
                                ["orderDate"] = new AttributeValue { S = newOrder.OrderDate.ToString("o") },
                                ["orderStatus"] = new AttributeValue { S = newOrder.Status },
                                ["totalAmount"] = new AttributeValue { N = newOrder.TotalAmount.ToString() }
                            }
                        }
                    },
                    // Put: Add first order line
                    new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = newLine1.Pk },
                                ["sk"] = new AttributeValue { S = newLine1.Sk },
                                ["lineId"] = new AttributeValue { S = newLine1.LineId },
                                ["productId"] = new AttributeValue { S = newLine1.ProductId },
                                ["productName"] = new AttributeValue { S = newLine1.ProductName },
                                ["quantity"] = new AttributeValue { N = newLine1.Quantity.ToString() },
                                ["unitPrice"] = new AttributeValue { N = newLine1.UnitPrice.ToString() }
                            }
                        }
                    },
                    // Put: Add second order line
                    new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = newLine2.Pk },
                                ["sk"] = new AttributeValue { S = newLine2.Sk },
                                ["lineId"] = new AttributeValue { S = newLine2.LineId },
                                ["productId"] = new AttributeValue { S = newLine2.ProductId },
                                ["productName"] = new AttributeValue { S = newLine2.ProductName },
                                ["quantity"] = new AttributeValue { N = newLine2.Quantity.ToString() },
                                ["unitPrice"] = new AttributeValue { N = newLine2.UnitPrice.ToString() }
                            }
                        }
                    },
                    // Delete: Remove an old order line
                    new WriteRequest
                    {
                        DeleteRequest = new DeleteRequest
                        {
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = $"ORDER#{deleteOrderId}" },
                                ["sk"] = new AttributeValue { S = $"LINE#{deleteLineId}" }
                            }
                        }
                    }
                }
            }
        };

        return await client.BatchWriteItemAsync(request);
    }

    /// <summary>
    /// FluentDynamoDb manual builder - uses explicit WithItem() and WithKey() methods.
    /// </summary>
    public static async Task<BatchWriteItemResponse> FluentManualBatchWriteAsync(
        OrdersTable table,
        Order newOrder,
        OrderLine newLine1,
        OrderLine newLine2,
        string deleteOrderId,
        string deleteLineId)
    {
        return await DynamoDbBatch.Write
            .Add(table.Put<Order>().WithItem(newOrder))
            .Add(table.Put<OrderLine>().WithItem(newLine1))
            .Add(table.Put<OrderLine>().WithItem(newLine2))
            .Add(table.Delete<OrderLine>().WithKey("pk", $"ORDER#{deleteOrderId}", "sk", $"LINE#{deleteLineId}"))
            .ExecuteAsync();
    }

    /// <summary>
    /// FluentDynamoDb formatted string - uses helper methods for key construction.
    /// </summary>
    public static async Task<BatchWriteItemResponse> FluentFormattedBatchWriteAsync(
        OrdersTable table,
        Order newOrder,
        OrderLine newLine1,
        OrderLine newLine2,
        string deleteOrderId,
        string deleteLineId)
    {
        return await DynamoDbBatch.Write
            .Add(table.Put<Order>().WithItem(newOrder))
            .Add(table.Put<OrderLine>().WithItem(newLine1))
            .Add(table.Put<OrderLine>().WithItem(newLine2))
            .Add(table.Delete<OrderLine>().WithKey("pk", OrderLine.CreatePk(deleteOrderId), "sk", OrderLine.CreateSk(deleteLineId)))
            .ExecuteAsync();
    }

    /// <summary>
    /// FluentDynamoDb lambda expression - uses entity accessors with strongly-typed operations.
    /// </summary>
    public static async Task<BatchWriteItemResponse> FluentLambdaBatchWriteAsync(
        OrdersTable table,
        Order newOrder,
        OrderLine newLine1,
        OrderLine newLine2,
        string deleteOrderId,
        string deleteLineId)
    {
        return await DynamoDbBatch.Write
            .Add(table.Orders.Put(newOrder))
            .Add(table.OrderLines.Put(newLine1))
            .Add(table.OrderLines.Put(newLine2))
            .Add(table.OrderLines.Delete(OrderLine.CreatePk(deleteOrderId), OrderLine.CreateSk(deleteLineId)))
            .ExecuteAsync();
    }
}
