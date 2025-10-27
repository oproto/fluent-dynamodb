using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

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

    /// <summary>
    /// Converts QueryResponse items to a list of strongly-typed entities (1:1 mapping).
    /// Each DynamoDB item becomes a separate entity instance in the returned list.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static List<T> ToList<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            return response.Items
                .Where(T.MatchesEntity)
                .Select(item => T.FromDynamoDb<T>(item))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts QueryResponse items to composite entities (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into single entity instances.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static List<T> ToCompositeEntityList<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            return matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First())
                    : T.FromDynamoDb<T>(group.ToList()))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts QueryResponse items to a single composite entity (N:1 mapping).
    /// All DynamoDB items are combined into a single entity instance.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <returns>A single composite entity constructed from multiple DynamoDB items, or null if no matching items found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToCompositeEntity<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            if (matchingItems.Count == 0)
                return null;

            return T.FromDynamoDb<T>(matchingItems);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region ScanResponse Extensions

    /// <summary>
    /// Converts ScanResponse items to a list of strongly-typed entities (1:1 mapping).
    /// Each DynamoDB item becomes a separate entity instance in the returned list.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The ScanResponse from AWS SDK.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static List<T> ToList<T>(this ScanResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            return response.Items
                .Where(T.MatchesEntity)
                .Select(item => T.FromDynamoDb<T>(item))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert ScanResponse items to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts ScanResponse items to composite entities (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into single entity instances.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The ScanResponse from AWS SDK.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static List<T> ToCompositeEntityList<T>(this ScanResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            return matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First())
                    : T.FromDynamoDb<T>(group.ToList()))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert ScanResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region GetItemResponse Extensions

    /// <summary>
    /// Converts GetItemResponse item to a strongly-typed entity.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The GetItemResponse from AWS SDK.</param>
    /// <returns>The mapped entity or null if not found or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToEntity<T>(this GetItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            if (response.Item == null || !T.MatchesEntity(response.Item))
                return null;

            return T.FromDynamoDb<T>(response.Item);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert GetItemResponse item to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region UpdateItemResponse Extensions

    /// <summary>
    /// Converts UpdateItemResponse Attributes (pre-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD or UPDATED_OLD.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The UpdateItemResponse from AWS SDK.</param>
    /// <returns>The mapped entity representing the state before the update, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToPreOperationEntity<T>(this UpdateItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            return T.FromDynamoDb<T>(response.Attributes);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert UpdateItemResponse pre-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts UpdateItemResponse Attributes (post-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_NEW or UPDATED_NEW.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The UpdateItemResponse from AWS SDK.</param>
    /// <returns>The mapped entity representing the state after the update, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToPostOperationEntity<T>(this UpdateItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            return T.FromDynamoDb<T>(response.Attributes);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert UpdateItemResponse post-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region DeleteItemResponse Extensions

    /// <summary>
    /// Converts DeleteItemResponse Attributes (pre-deletion values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The DeleteItemResponse from AWS SDK.</param>
    /// <returns>The mapped entity representing the state before deletion, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToPreOperationEntity<T>(this DeleteItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            return T.FromDynamoDb<T>(response.Attributes);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert DeleteItemResponse pre-deletion attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region PutItemResponse Extensions

    /// <summary>
    /// Converts PutItemResponse Attributes (pre-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The PutItemResponse from AWS SDK.</param>
    /// <returns>The mapped entity representing the state before the put operation, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static T? ToPreOperationEntity<T>(this PutItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            return T.FromDynamoDb<T>(response.Attributes);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert PutItemResponse pre-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region QueryResponse Extensions with Blob Provider

    /// <summary>
    /// Converts QueryResponse items to a list of strongly-typed entities (1:1 mapping) with blob reference support.
    /// Each DynamoDB item becomes a separate entity instance in the returned list.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this QueryResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method for each item
                var tasks = matchingItems.Select(async item =>
                {
                    var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { item, blobProvider, cancellationToken })!;
                    return await task;
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return matchingItems.Select(item => T.FromDynamoDb<T>(item)).ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts QueryResponse items to composite entities (N:1 mapping) with blob reference support.
    /// Multiple DynamoDB items with the same partition key are combined into single entity instances.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this QueryResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            // Check if entity has async FromDynamoDb method for multi-item
            var fromDynamoDbMultiAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(IList<Dictionary<string, AttributeValue>>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            // Check if entity has async FromDynamoDb method for single-item
            var fromDynamoDbSingleAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            // Group items by partition key for multi-item entities
            var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();

            if (fromDynamoDbMultiAsyncMethod != null && fromDynamoDbSingleAsyncMethod != null)
            {
                // Entity has blob references - use async methods
                var tasks = groups.Select(async group =>
                {
                    if (group.Count() == 1)
                    {
                        var task = (Task<T>)fromDynamoDbSingleAsyncMethod.Invoke(null, new object[] { group.First(), blobProvider, cancellationToken })!;
                        return await task;
                    }
                    else
                    {
                        var task = (Task<T>)fromDynamoDbMultiAsyncMethod.Invoke(null, new object[] { group.ToList(), blobProvider, cancellationToken })!;
                        return await task;
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous methods
                return groups.Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First())
                    : T.FromDynamoDb<T>(group.ToList()))
                    .ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts QueryResponse items to a single composite entity (N:1 mapping) with blob reference support.
    /// All DynamoDB items are combined into a single entity instance.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The QueryResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A single composite entity constructed from multiple DynamoDB items, or null if no matching items found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToCompositeEntityAsync<T>(
        this QueryResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            if (matchingItems.Count == 0)
                return null;

            // Check if entity has async FromDynamoDb method for multi-item
            var fromDynamoDbMultiAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(IList<Dictionary<string, AttributeValue>>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbMultiAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbMultiAsyncMethod.Invoke(null, new object[] { matchingItems, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(matchingItems);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert QueryResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region ScanResponse Extensions with Blob Provider

    /// <summary>
    /// Converts ScanResponse items to a list of strongly-typed entities (1:1 mapping) with blob reference support.
    /// Each DynamoDB item becomes a separate entity instance in the returned list.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The ScanResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this ScanResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method for each item
                var tasks = matchingItems.Select(async item =>
                {
                    var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { item, blobProvider, cancellationToken })!;
                    return await task;
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return matchingItems.Select(item => T.FromDynamoDb<T>(item)).ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert ScanResponse items to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts ScanResponse items to composite entities (N:1 mapping) with blob reference support.
    /// Multiple DynamoDB items with the same partition key are combined into single entity instances.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The ScanResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this ScanResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();

            // Check if entity has async FromDynamoDb method for multi-item
            var fromDynamoDbMultiAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(IList<Dictionary<string, AttributeValue>>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            // Check if entity has async FromDynamoDb method for single-item
            var fromDynamoDbSingleAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            // Group items by partition key for multi-item entities
            var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();

            if (fromDynamoDbMultiAsyncMethod != null && fromDynamoDbSingleAsyncMethod != null)
            {
                // Entity has blob references - use async methods
                var tasks = groups.Select(async group =>
                {
                    if (group.Count() == 1)
                    {
                        var task = (Task<T>)fromDynamoDbSingleAsyncMethod.Invoke(null, new object[] { group.First(), blobProvider, cancellationToken })!;
                        return await task;
                    }
                    else
                    {
                        var task = (Task<T>)fromDynamoDbMultiAsyncMethod.Invoke(null, new object[] { group.ToList(), blobProvider, cancellationToken })!;
                        return await task;
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous methods
                return groups.Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First())
                    : T.FromDynamoDb<T>(group.ToList()))
                    .ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert ScanResponse items to composite {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region GetItemResponse Extensions with Blob Provider

    /// <summary>
    /// Converts GetItemResponse item to a strongly-typed entity with blob reference support.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The GetItemResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity or null if not found or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToEntityAsync<T>(
        this GetItemResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            if (response.Item == null || !T.MatchesEntity(response.Item))
                return null;

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { response.Item, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Item);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert GetItemResponse item to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region UpdateItemResponse Extensions with Blob Provider

    /// <summary>
    /// Converts UpdateItemResponse Attributes (pre-operation values) to a strongly-typed entity with blob reference support.
    /// Only applicable when ReturnValues is set to ALL_OLD or UPDATED_OLD.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The UpdateItemResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity representing the state before the update, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToPreOperationEntityAsync<T>(
        this UpdateItemResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { response.Attributes, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Attributes);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert UpdateItemResponse pre-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts UpdateItemResponse Attributes (post-operation values) to a strongly-typed entity with blob reference support.
    /// Only applicable when ReturnValues is set to ALL_NEW or UPDATED_NEW.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The UpdateItemResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity representing the state after the update, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToPostOperationEntityAsync<T>(
        this UpdateItemResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { response.Attributes, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Attributes);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert UpdateItemResponse post-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region DeleteItemResponse Extensions with Blob Provider

    /// <summary>
    /// Converts DeleteItemResponse Attributes (pre-deletion values) to a strongly-typed entity with blob reference support.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The DeleteItemResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity representing the state before deletion, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToPreOperationEntityAsync<T>(
        this DeleteItemResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { response.Attributes, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Attributes);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert DeleteItemResponse pre-deletion attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion

    #region PutItemResponse Extensions with Blob Provider

    /// <summary>
    /// Converts PutItemResponse Attributes (pre-operation values) to a strongly-typed entity with blob reference support.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method does NOT populate DynamoDbOperationContext.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="response">The PutItemResponse from AWS SDK.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity representing the state before the put operation, or null if not available or doesn't match entity type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when response or blobProvider is null.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToPreOperationEntityAsync<T>(
        this PutItemResponse response,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider));

        try
        {
            if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
                return null;

            // Check if entity has async FromDynamoDb method
            var fromDynamoDbAsyncMethod = typeof(T).GetMethod(
                "FromDynamoDbAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Dictionary<string, AttributeValue>), typeof(IBlobStorageProvider), typeof(CancellationToken) },
                null);

            if (fromDynamoDbAsyncMethod != null)
            {
                // Entity has blob references - use async method
                var task = (Task<T>)fromDynamoDbAsyncMethod.Invoke(null, new object[] { response.Attributes, blobProvider, cancellationToken })!;
                return await task;
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Attributes);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert PutItemResponse pre-operation attributes to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    #endregion
}
