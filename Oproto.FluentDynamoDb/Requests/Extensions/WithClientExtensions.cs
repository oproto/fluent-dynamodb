using Amazon.DynamoDBv2;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extensions to support scoped client usage with request builders.
/// These extensions allow service layers to provide tenant-specific STS clients.
/// </summary>
public static class WithClientExtensions
{
    /// <summary>
    /// Creates a new GetItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original GetItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new GetItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static GetItemRequestBuilder WithClient(this GetItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new GetItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToGetItemRequest();
        var newBuilderRequestField = typeof(GetItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute name mappings
        var originalAttrN = builder.GetAttributeNameHelper();
        var newBuilderAttrNField = typeof(GetItemRequestBuilder).GetField("_attrN", BindingFlags.NonPublic | BindingFlags.Instance);
        var newAttrN = (AttributeNameInternal?)newBuilderAttrNField?.GetValue(newBuilder);
        if (newAttrN != null && originalAttrN.AttributeNames != null)
        {
            foreach (var kvp in originalAttrN.AttributeNames)
            {
                newAttrN.AttributeNames[kvp.Key] = kvp.Value;
            }
        }
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new QueryRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original QueryRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new QueryRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static QueryRequestBuilder WithClient(this QueryRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new QueryRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToQueryRequest();
        var newBuilderRequestField = typeof(QueryRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new PutItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original PutItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new PutItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static PutItemRequestBuilder WithClient(this PutItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new PutItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToPutItemRequest();
        var newBuilderRequestField = typeof(PutItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new UpdateItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original UpdateItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new UpdateItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static UpdateItemRequestBuilder WithClient(this UpdateItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new UpdateItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToUpdateItemRequest();
        var newBuilderRequestField = typeof(UpdateItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new DeleteItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original DeleteItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new DeleteItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static DeleteItemRequestBuilder WithClient(this DeleteItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new DeleteItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToDeleteItemRequest();
        var newBuilderRequestField = typeof(DeleteItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new ScanRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original ScanRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new ScanRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static ScanRequestBuilder WithClient(this ScanRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new ScanRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToScanRequest();
        var newBuilderRequestField = typeof(ScanRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new BatchGetItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original BatchGetItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new BatchGetItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static BatchGetItemRequestBuilder WithClient(this BatchGetItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new BatchGetItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToBatchGetItemRequest();
        var newBuilderRequestField = typeof(BatchGetItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new BatchWriteItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original BatchWriteItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new BatchWriteItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static BatchWriteItemRequestBuilder WithClient(this BatchWriteItemRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new BatchWriteItemRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToBatchWriteItemRequest();
        var newBuilderRequestField = typeof(BatchWriteItemRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new TransactWriteItemsRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original TransactWriteItemsRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new TransactWriteItemsRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static TransactWriteItemsRequestBuilder WithClient(this TransactWriteItemsRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new TransactWriteItemsRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToTransactWriteItemsRequest();
        var newBuilderRequestField = typeof(TransactWriteItemsRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        return newBuilder;
    }

    /// <summary>
    /// Creates a new TransactGetItemsRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <param name="builder">The original TransactGetItemsRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new TransactGetItemsRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static TransactGetItemsRequestBuilder WithClient(this TransactGetItemsRequestBuilder builder, IAmazonDynamoDB client)
    {
        var newBuilder = new TransactGetItemsRequestBuilder(client);
        
        // Copy the request configuration
        var originalRequest = builder.ToTransactGetItemsRequest();
        var newBuilderRequestField = typeof(TransactGetItemsRequestBuilder).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);
        
        return newBuilder;
    }

    /// <summary>
    /// Helper method to copy attribute value and name helpers between builders.
    /// </summary>
    private static void CopyAttributeHelpers<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] T>(AttributeValueInternal originalAttrV, AttributeNameInternal originalAttrN, T newBuilder)
    {
        // Copy attribute value mappings
        var newBuilderAttrVField = typeof(T).GetField("_attrV", BindingFlags.NonPublic | BindingFlags.Instance);
        var newAttrV = (AttributeValueInternal?)newBuilderAttrVField?.GetValue(newBuilder);
        if (newAttrV != null && originalAttrV.AttributeValues != null)
        {
            foreach (var kvp in originalAttrV.AttributeValues)
            {
                newAttrV.AttributeValues[kvp.Key] = kvp.Value;
            }
        }

        // Copy attribute name mappings
        var newBuilderAttrNField = typeof(T).GetField("_attrN", BindingFlags.NonPublic | BindingFlags.Instance);
        var newAttrN = (AttributeNameInternal?)newBuilderAttrNField?.GetValue(newBuilder);
        if (newAttrN != null && originalAttrN.AttributeNames != null)
        {
            foreach (var kvp in originalAttrN.AttributeNames)
            {
                newAttrN.AttributeNames[kvp.Key] = kvp.Value;
            }
        }
    }
}