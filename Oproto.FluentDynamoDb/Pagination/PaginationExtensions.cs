using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
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
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static QueryRequestBuilder<TEntity> Paginate<TEntity>(this QueryRequestBuilder<TEntity> builder, IPaginationRequest request) where TEntity : class
    {
        Dictionary<string, AttributeValue>? startAt = null;

        if (!String.IsNullOrWhiteSpace(request.PaginationToken))
        {
            try
            {
                startAt = JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(
                    Convert.FromBase64String(request.PaginationToken), SerializationContext.Default.DictionaryStringAttributeValue);
                foreach (var key in startAt!.Keys)
                {
                    // Bug fix for deserialization of AttributeValue from DynamoDb
                    GetAttributeValueNullField(startAt[key]) = null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        if (startAt != null && request.PageSize != 0)
        {
            return builder.StartAt(startAt).Take(request.PageSize);
        }
        else if (startAt == null && request.PageSize != 0)
        {
            return builder.Take(request.PageSize);
        }
        else
        {
            return builder;
        }
    }

    /// <summary>
    /// Generates a base64-encoded pagination token from a QueryResponse's LastEvaluatedKey.
    /// This token can be used in subsequent requests to continue pagination from where this query left off.
    /// The encoding is AOT-compatible using System.Text.Json with a serialization context.
    /// </summary>
    /// <param name="queryResponse">The QueryResponse containing the LastEvaluatedKey.</param>
    /// <returns>A base64-encoded pagination token, or empty string if there are no more pages.</returns>
    /// <example>
    /// <code>
    /// var response = await query.ExecuteAsync();
    /// var nextToken = response.GetEncodedPaginationToken();
    /// // Use nextToken in the next pagination request
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Using Serialization Context")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Using Serialization Context")]
    public static string GetEncodedPaginationToken(this QueryResponse queryResponse)
    {
        // Override defaults to have the smallest serialization possible
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;

        // Use Serialization Context for AOT compatibility
        options.TypeInfoResolver = SerializationContext.Default.DictionaryStringAttributeValue
            .OriginatingResolver;

        var lastEvaluationKey = JsonSerializer.Serialize(queryResponse.LastEvaluatedKey, options);
        var lastEvaluationKeyBytes = Encoding.UTF8.GetBytes(lastEvaluationKey);

        return System.Convert.ToBase64String(lastEvaluationKeyBytes);
    }
}