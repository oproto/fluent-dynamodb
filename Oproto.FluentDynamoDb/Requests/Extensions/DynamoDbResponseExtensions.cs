using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for AWS DynamoDB response objects to enable entity deserialization.
/// These methods are designed for advanced API users who work with raw AWS SDK responses
/// via the ToDynamoDbResponseAsync() methods. They provide convenient conversion from
/// raw AttributeValue dictionaries to strongly-typed entities without populating the
/// DynamoDbOperationContext.
/// 
/// For most use cases, prefer the Primary API methods (GetItemAsync, ToListAsync, etc.)
/// which automatically handle deserialization and populate operation context.
/// </summary>
public static class DynamoDbResponseExtensions
{
    #region QueryResponse Extensions

    #endregion

    #region ScanResponse Extensions with Blob Provider

    #endregion

    #region GetItemResponse Extensions with Blob Provider

    #endregion

    #region UpdateItemResponse Extensions with Blob Provider

    #endregion

    #region DeleteItemResponse Extensions with Blob Provider

    #endregion

    #region PutItemResponse Extensions with Blob Provider

    #endregion
}
