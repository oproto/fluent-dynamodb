using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.Pagination;

/// <summary>
/// Extension methods for implementing pagination with DynamoDB Query operations.
/// Provides AOT-compatible pagination token encoding/decoding using System.Text.Json.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Provides access to the private _null field in AttributeValue to fix a deserialization bug in the AWS SDK.
    /// This is required for AOT compatibility as we cannot use reflection.
    /// Uses the .NET 8.0+ UnsafeAccessor feature to access private fields safely.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_null")]
    static extern ref bool? GetAttributeValueNullField(AttributeValue @this);

    /// <summary>
    /// Configures a QueryRequestBuilder with pagination parameters.
    /// Automatically handles pagination token decoding and applies the appropriate StartAt and Take settings.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The QueryRequestBuilder to configure.</param>
    /// <param name="request">The pagination request containing page size and token.</param>
    /// <returns>The configured QueryRequestBuilder.</returns>
    /// <example>
    /// <code>
    /// var paginationRequest = new PaginationRequest(10, previousToken);
    /// var response = await table.Query&lt;MyEntity&gt;()
    ///     .Where("pk = :pk")
    ///     .WithValue(":pk", "USER#123")
    ///     .Paginate(paginationRequest)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public static QueryRequestBuilder<TEntity> Paginate<TEntity>(this QueryRequestBuilder<TEntity> builder, IPaginationRequest request) 
        where TEntity : class, IReadOnlyEntity
    {
        var startAt = DecodePaginationToken(request.PaginationToken);

        if (startAt != null && request.PageSize != 0)
        {
            builder.StartAt(startAt).Take(request.PageSize);
        }
        else if (request.PageSize != 0)
        {
            builder.Take(request.PageSize);
        }

        return builder;
    }

    /// <summary>
    /// Configures a ScanRequestBuilder with pagination parameters.
    /// Automatically handles pagination token decoding and applies the appropriate StartAt and Take settings.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being scanned.</typeparam>
    /// <param name="builder">The ScanRequestBuilder to configure.</param>
    /// <param name="request">The pagination request containing page size and token.</param>
    /// <returns>The configured ScanRequestBuilder.</returns>
    /// <example>
    /// <code>
    /// var paginationRequest = new PaginationRequest(10, previousToken);
    /// var response = await table.Scan&lt;MyEntity&gt;()
    ///     .Paginate(paginationRequest)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public static ScanRequestBuilder<TEntity> Paginate<TEntity>(this ScanRequestBuilder<TEntity> builder, IPaginationRequest request) 
        where TEntity : class, IReadOnlyEntity
    {
        var startAt = DecodePaginationToken(request.PaginationToken);

        if (startAt != null && request.PageSize != 0)
        {
            builder.StartAt(startAt).Take(request.PageSize);
        }
        else if (request.PageSize != 0)
        {
            builder.Take(request.PageSize);
        }

        return builder;
    }

    /// <summary>
    /// Decodes a base64-encoded pagination token into a LastEvaluatedKey dictionary.
    /// </summary>
    /// <param name="paginationToken">The base64-encoded pagination token.</param>
    /// <returns>The decoded LastEvaluatedKey dictionary, or null if the token is empty.</returns>
    private static Dictionary<string, AttributeValue>? DecodePaginationToken(string? paginationToken)
    {
        if (String.IsNullOrWhiteSpace(paginationToken))
        {
            return null;
        }

        try
        {
            var startAt = JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(
                Convert.FromBase64String(paginationToken), SerializationContext.Default.DictionaryStringAttributeValue);
            
            if (startAt != null)
            {
                foreach (var key in startAt.Keys)
                {
                    // Bug fix for deserialization of AttributeValue from DynamoDb
                    GetAttributeValueNullField(startAt[key]) = null;
                }
            }
            
            return startAt;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Generates a base64-encoded pagination token from a QueryOperationResponse's LastEvaluatedKey.
    /// This token can be used in subsequent requests to continue pagination from where this query left off.
    /// The encoding is AOT-compatible using System.Text.Json with a serialization context.
    /// </summary>
    /// <param name="response">The QueryOperationResponse containing the LastEvaluatedKey.</param>
    /// <returns>A base64-encoded pagination token, or empty string if there are no more pages.</returns>
    /// <example>
    /// <code>
    /// var items = await query.ToListAsync();
    /// var nextToken = query.Response?.GetEncodedPaginationToken() ?? string.Empty;
    /// // Use nextToken in the next pagination request
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    public static string GetEncodedPaginationToken(this QueryOperationResponse response)
    {
        if (response.LastEvaluatedKey == null || response.LastEvaluatedKey.Count == 0)
            return string.Empty;

        return EncodeLastEvaluatedKey(response.LastEvaluatedKey);
    }

    /// <summary>
    /// Generates a base64-encoded pagination token from a ScanOperationResponse's LastEvaluatedKey.
    /// This token can be used in subsequent requests to continue pagination from where this scan left off.
    /// The encoding is AOT-compatible using System.Text.Json with a serialization context.
    /// </summary>
    /// <param name="response">The ScanOperationResponse containing the LastEvaluatedKey.</param>
    /// <returns>A base64-encoded pagination token, or empty string if there are no more pages.</returns>
    /// <example>
    /// <code>
    /// var items = await scan.ToListAsync();
    /// var nextToken = scan.Response?.GetEncodedPaginationToken() ?? string.Empty;
    /// // Use nextToken in the next pagination request
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    public static string GetEncodedPaginationToken(this ScanOperationResponse response)
    {
        if (response.LastEvaluatedKey == null || response.LastEvaluatedKey.Count == 0)
            return string.Empty;

        return EncodeLastEvaluatedKey(response.LastEvaluatedKey);
    }

    /// <summary>
    /// Generates a base64-encoded pagination token from a QueryResponse's LastEvaluatedKey.
    /// This token can be used in subsequent requests to continue pagination from where this query left off.
    /// The encoding is AOT-compatible using System.Text.Json with a serialization context.
    /// </summary>
    /// <param name="queryResponse">The QueryResponse containing the LastEvaluatedKey.</param>
    /// <returns>A base64-encoded pagination token, or empty string if there are no more pages.</returns>
    /// <remarks>
    /// This overload accepts the raw AWS SDK QueryResponse. For most use cases, prefer using
    /// the QueryOperationResponse overload via builder.Response?.GetEncodedPaginationToken().
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    public static string GetEncodedPaginationToken(this QueryResponse queryResponse)
    {
        if (queryResponse.LastEvaluatedKey == null || queryResponse.LastEvaluatedKey.Count == 0)
            return string.Empty;

        return EncodeLastEvaluatedKey(queryResponse.LastEvaluatedKey);
    }

    /// <summary>
    /// Generates a base64-encoded pagination token from a ScanResponse's LastEvaluatedKey.
    /// This token can be used in subsequent requests to continue pagination from where this scan left off.
    /// The encoding is AOT-compatible using System.Text.Json with a serialization context.
    /// </summary>
    /// <param name="scanResponse">The ScanResponse containing the LastEvaluatedKey.</param>
    /// <returns>A base64-encoded pagination token, or empty string if there are no more pages.</returns>
    /// <remarks>
    /// This overload accepts the raw AWS SDK ScanResponse. For most use cases, prefer using
    /// the ScanOperationResponse overload via builder.Response?.GetEncodedPaginationToken().
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    public static string GetEncodedPaginationToken(this ScanResponse scanResponse)
    {
        if (scanResponse.LastEvaluatedKey == null || scanResponse.LastEvaluatedKey.Count == 0)
            return string.Empty;

        return EncodeLastEvaluatedKey(scanResponse.LastEvaluatedKey);
    }

    /// <summary>
    /// Encodes a LastEvaluatedKey dictionary to a base64 pagination token.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    private static string EncodeLastEvaluatedKey(Dictionary<string, AttributeValue> lastEvaluatedKey)
    {
        // Override defaults to have the smallest serialization possible
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;

        // Use Serialization Context for AOT compatibility
        options.TypeInfoResolver = SerializationContext.Default.DictionaryStringAttributeValue
            .OriginatingResolver;

        var lastEvaluationKey = JsonSerializer.Serialize(lastEvaluatedKey, options);
        var lastEvaluationKeyBytes = Encoding.UTF8.GetBytes(lastEvaluationKey);

        return Convert.ToBase64String(lastEvaluationKeyBytes);
    }
}