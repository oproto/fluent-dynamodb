using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;
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
    public static GetItemRequestBuilder<TEntity> WithClient<TEntity>(
        this GetItemRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new GetItemRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToGetItemRequest();
        var newBuilderRequestField = typeof(GetItemRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);

        // Copy attribute name mappings
        var originalAttrN = builder.GetAttributeNameHelper();
        var newBuilderAttrNField = typeof(GetItemRequestBuilder<TEntity>).GetField("_attrN", BindingFlags.NonPublic | BindingFlags.Instance);
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
    public static QueryRequestBuilder<TEntity> WithClient<TEntity>(
        this QueryRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new QueryRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToQueryRequest();
        var newBuilderRequestField = typeof(QueryRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);

        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);

        return newBuilder;
    }

    /// <summary>
    /// Creates a new PutItemRequestBuilder instance with a different DynamoDB client.
    /// This preserves all existing configuration from the original builder.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being put.</typeparam>
    /// <param name="builder">The original PutItemRequestBuilder instance.</param>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>A new PutItemRequestBuilder instance using the specified client with preserved configuration.</returns>
    public static PutItemRequestBuilder<TEntity> WithClient<TEntity>(
        this PutItemRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new PutItemRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToPutItemRequest();
        var newBuilderRequestField = typeof(PutItemRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
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
    public static UpdateItemRequestBuilder<TEntity> WithClient<TEntity>(
        this UpdateItemRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new UpdateItemRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToUpdateItemRequest();
        var newBuilderRequestField = typeof(UpdateItemRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
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
    public static DeleteItemRequestBuilder<TEntity> WithClient<TEntity>(
        this DeleteItemRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new DeleteItemRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToDeleteItemRequest();
        var newBuilderRequestField = typeof(DeleteItemRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
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
    public static ScanRequestBuilder<TEntity> WithClient<TEntity>(
        this ScanRequestBuilder<TEntity> builder, 
        IAmazonDynamoDB client)
        where TEntity : class
    {
        var newBuilder = new ScanRequestBuilder<TEntity>(client);

        // Copy the request configuration
        var originalRequest = builder.ToScanRequest();
        var newBuilderRequestField = typeof(ScanRequestBuilder<TEntity>).GetField("_req", BindingFlags.NonPublic | BindingFlags.Instance);
        newBuilderRequestField?.SetValue(newBuilder, originalRequest);

        // Copy attribute mappings
        CopyAttributeHelpers(builder.GetAttributeValueHelper(), builder.GetAttributeNameHelper(), newBuilder);

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