using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.Pagination;

public static class PaginationExtensions
{
    // There is a bug in the deserialization of AttributeValue in AWS DynamoDb SDK surrounding the _null private
    // field being set to true instead of null.  To be AOT compatible, we cannot use reflection to fix.
    // Instead, use the .NET 8.0+ feature of UnsafeAccessor to gain access and fix it.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_null")]
    static extern ref bool? GetAttributeValueNullField(AttributeValue @this);
    
    public static QueryRequestBuilder Paginate(this QueryRequestBuilder builder, IPaginationRequest request)
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