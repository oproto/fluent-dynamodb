using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Enhanced ExecuteAsync extensions that provide strongly-typed entity mapping.
/// These extensions work with entities that implement IDynamoDbEntity interface.
/// </summary>
public static class EnhancedExecuteAsyncExtensions
{
    /// <summary>
    /// Executes a GetItem operation and returns a strongly-typed entity (Primary API).
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity or null if not found.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> GetItemAsync<T>(
        this GetItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToGetItemRequest();
            var response = await builder.GetDynamoDbClient().GetItemAsync(request, cancellationToken);

            // Populate context with GetItemResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "GetItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                RawItem = response.Item,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Return POCO (nullable)
            if (response.Item == null || !T.MatchesEntity(response.Item))
                return null;

            return T.FromDynamoDb<T>(response.Item, builder.GetOptions());
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute GetItem operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a GetItem operation and returns a strongly-typed entity with blob reference support (Primary API).
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The mapped entity or null if not found.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<T?> GetItemAsync<T>(
        this GetItemRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToGetItemRequest();
            var response = await builder.GetDynamoDbClient().GetItemAsync(request, cancellationToken);

            // Populate context with GetItemResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "GetItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                RawItem = response.Item,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Return POCO (nullable)
            if (response.Item == null || !T.MatchesEntity(response.Item))
                return null;

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var hydrator = builder.GetOptions().HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                return await hydrator.HydrateAsync(response.Item, blobProvider, builder.GetOptions(), cancellationToken);
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(response.Item, builder.GetOptions());
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute GetItem operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and maps each DynamoDB item to a separate entity instance (1:1 mapping).
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Use this method when you want to work with individual items as separate entities.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// It also populates LastEvaluatedKey and ScannedCount on the builder instance for direct access.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate builder with response metadata for direct access (avoids AsyncLocal issues)
            builder.LastEvaluatedKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null;
            builder.ScannedCount = response.ScannedCount;

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Each DynamoDB item becomes a separate T instance (1:1 mapping)
            var options = builder.GetOptions();
            var entityItems = items
                .Where(T.MatchesEntity)
                .Select(item => T.FromDynamoDb<T>(item, options))
                .ToList();

            return entityItems;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and maps each DynamoDB item to a separate entity instance (1:1 mapping) with blob reference support.
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// It also populates LastEvaluatedKey and ScannedCount on the builder instance for direct access.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this QueryRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate builder with response metadata for direct access (avoids AsyncLocal issues)
            builder.LastEvaluatedKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null;
            builder.ScannedCount = response.ScannedCount;

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Filter matching items
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                var tasks = matchingItems.Select(item => hydrator.HydrateAsync(item, blobProvider, options, cancellationToken));
                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return matchingItems.Select(item => T.FromDynamoDb<T>(item, options)).ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and combines multiple DynamoDB items into composite entities (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Primary entity is identified by sort key patterns, related entities populate properties using [RelatedEntity] attributes.
    /// Use this method when you want to work with composite entities that span multiple DynamoDB items.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Group items by partition key for multi-item entities
            var options = builder.GetOptions();
            var entityItems = matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First(), options)
                    : T.FromDynamoDb<T>(group.ToList(), options))
                .ToList();

            return entityItems;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and combines multiple DynamoDB items into composite entities (N:1 mapping) with blob reference support.
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this QueryRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Group items by partition key for multi-item entities
            var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                var tasks = groups.Select(async group =>
                {
                    if (group.Count() == 1)
                    {
                        return await hydrator.HydrateAsync(group.First(), blobProvider, options, cancellationToken);
                    }
                    else
                    {
                        return await hydrator.HydrateAsync(group.ToList(), blobProvider, options, cancellationToken);
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous methods
                return groups.Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First(), options)
                    : T.FromDynamoDb<T>(group.ToList(), options))
                    .ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and returns a single composite entity (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into a single T instance.
    /// Primary entity is identified by sort key patterns, related entities populate properties using [RelatedEntity] attributes.
    /// Use this method when you expect to get a single composite entity from the query.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A single composite entity constructed from multiple DynamoDB items, or null if no matching items found.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<T?> ToCompositeEntityAsync<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            if (matchingItems.Count == 0)
                return null;

            // Use multi-item FromDynamoDb to combine all items into single entity
            return T.FromDynamoDb<T>(matchingItems, builder.GetOptions());
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and returns a single composite entity (N:1 mapping) with blob reference support.
    /// Multiple DynamoDB items with the same partition key are combined into a single T instance.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A single composite entity constructed from multiple DynamoDB items, or null if no matching items found.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<T?> ToCompositeEntityAsync<T>(
        this QueryRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToQueryRequest();
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with QueryResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Query",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            if (matchingItems.Count == 0)
                return null;

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                return await hydrator.HydrateAsync(matchingItems, blobProvider, options, cancellationToken);
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return T.FromDynamoDb<T>(matchingItems, options);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Query operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configures the PutItem operation to use a strongly-typed entity.
    /// The entity is automatically converted to DynamoDB AttributeValue format.
    /// For multi-item entities, only the first item is used for PutItem operations.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="item">The entity instance to put.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity conversion fails.</exception>
    public static PutItemRequestBuilder<T> WithItem<T>(
        this PutItemRequestBuilder<T> builder,
        T item)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var attributeDict = T.ToDynamoDb(item, builder.GetOptions());
            return builder.WithItem(attributeDict);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert {typeof(T).Name} entity to DynamoDB format. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configures the PutItem operation to use a strongly-typed entity with blob reference support.
    /// The entity is automatically converted to DynamoDB AttributeValue format.
    /// Blob properties are stored externally and only references are saved in DynamoDB.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="item">The entity instance to put.</param>
    /// <param name="blobProvider">The blob storage provider for storing blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that resolves to the builder instance for method chaining.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity conversion fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<PutItemRequestBuilder<T>> WithItemAsync<T>(
        this PutItemRequestBuilder<T> builder,
        T item,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            Dictionary<string, AttributeValue> attributeDict;

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator's serialize method (no reflection)
                attributeDict = await hydrator.SerializeAsync(item, blobProvider, options, cancellationToken);
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                attributeDict = T.ToDynamoDb(item, options);
            }

            return builder.WithItem(attributeDict);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to convert {typeof(T).Name} entity to DynamoDB format. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Scan operation and maps each DynamoDB item to a separate entity instance (1:1 mapping).
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this ScanRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToScanRequest();
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with ScanResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Scan",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Each DynamoDB item becomes a separate T instance (1:1 mapping)
            var options = builder.GetOptions();
            var entityItems = items
                .Where(T.MatchesEntity)
                .Select(item => T.FromDynamoDb<T>(item, options))
                .ToList();

            return entityItems;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Scan operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Scan operation and maps each DynamoDB item to a separate entity instance (1:1 mapping) with blob reference support.
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of mapped entities, one per DynamoDB item.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<List<T>> ToListAsync<T>(
        this ScanRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToScanRequest();
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with ScanResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Scan",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Filter matching items
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                var tasks = matchingItems.Select(item => hydrator.HydrateAsync(item, blobProvider, options, cancellationToken));
                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous method
                return matchingItems.Select(item => T.FromDynamoDb<T>(item, options)).ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Scan operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Scan operation and combines multiple DynamoDB items into composite entities (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this ScanRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToScanRequest();
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with ScanResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Scan",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Group items by partition key for multi-item entities
            var options = builder.GetOptions();
            var entityItems = matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First(), options)
                    : T.FromDynamoDb<T>(group.ToList(), options))
                .ToList();

            return entityItems;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Scan operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Scan operation and combines multiple DynamoDB items into composite entities (N:1 mapping) with blob reference support.
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of composite entities, where each entity may be constructed from multiple DynamoDB items.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task<List<T>> ToCompositeEntityListAsync<T>(
        this ScanRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of ExecuteAsync()
            var request = builder.ToScanRequest();
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate context with ScanResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "Scan",
                TableName = request.TableName,
                IndexName = request.IndexName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCount = response.Count,
                ScannedCount = response.ScannedCount,
                LastEvaluatedKey = response.LastEvaluatedKey,
                RawItems = items,
                ResponseMetadata = response.ResponseMetadata
            };

            // Filter items that match the entity type
            var matchingItems = items.Where(T.MatchesEntity).ToList();

            // Group items by partition key for multi-item entities
            var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();

            // Check if a hydrator is registered for this entity type (AOT-safe)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry.GetHydrator<T>();
            if (hydrator != null)
            {
                // Entity has blob references - use registered hydrator (no reflection)
                var tasks = groups.Select(async group =>
                {
                    if (group.Count() == 1)
                    {
                        return await hydrator.HydrateAsync(group.First(), blobProvider, options, cancellationToken);
                    }
                    else
                    {
                        return await hydrator.HydrateAsync(group.ToList(), blobProvider, options, cancellationToken);
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                // Entity doesn't have blob references - use synchronous methods
                return groups.Select(group => group.Count() == 1
                    ? T.FromDynamoDb<T>(group.First(), options)
                    : T.FromDynamoDb<T>(group.ToList(), options))
                    .ToList();
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Scan operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a PutItem operation and stores the entity in DynamoDB (Primary API).
    /// This method populates DynamoDbOperationContext.Current with operation metadata including PreOperationValues.
    /// PutItem creates a new item or completely replaces an existing item with the same primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static async Task PutAsync<T>(
        this PutItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToPutItemRequest();
            var response = await builder.GetDynamoDbClient().PutItemAsync(request, cancellationToken);

            // Populate context with PutItemResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "PutItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCollectionMetrics = response.ItemCollectionMetrics,
                PreOperationValues = response.Attributes, // If ReturnValues was set to ALL_OLD
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute PutItem operation. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a PutItem operation with blob reference support and stores the entity in DynamoDB (Primary API).
    /// This method populates DynamoDbOperationContext.Current with operation metadata including PreOperationValues.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for storing blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    /// <exception cref="ArgumentNullException">Thrown when blobProvider is null.</exception>
    public static async Task PutAsync<T>(
        this PutItemRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (blobProvider == null)
            throw new ArgumentNullException(nameof(blobProvider), "Blob provider is required for entities with blob reference properties");

        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToPutItemRequest();
            var response = await builder.GetDynamoDbClient().PutItemAsync(request, cancellationToken);

            // Populate context with PutItemResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "PutItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCollectionMetrics = response.ItemCollectionMetrics,
                PreOperationValues = response.Attributes, // If ReturnValues was set to ALL_OLD
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute PutItem operation. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes an UpdateItem operation and modifies the entity in DynamoDB (Primary API).
    /// This method populates DynamoDbOperationContext.Current with operation metadata including Pre/PostOperationValues.
    /// UpdateItem modifies existing items or creates them if they don't exist (upsert behavior).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static async Task UpdateAsync<T>(
        this UpdateItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            // Use ToDynamoDbResponseAsync which handles encryption and execution
            var response = await builder.ToDynamoDbResponseAsync(cancellationToken);
            
            // Get the request after encryption to check ReturnValues setting
            var request = builder.ToUpdateItemRequest();

            // Populate context with UpdateItemResponse metadata
            // Note: Attributes contains either pre-operation values (ALL_OLD/UPDATED_OLD) or post-operation values (ALL_NEW/UPDATED_NEW)
            // depending on the ReturnValues setting
            var isPreOperation = request.ReturnValues == ReturnValue.ALL_OLD || request.ReturnValues == ReturnValue.UPDATED_OLD;
            var isPostOperation = request.ReturnValues == ReturnValue.ALL_NEW || request.ReturnValues == ReturnValue.UPDATED_NEW;

            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "UpdateItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCollectionMetrics = response.ItemCollectionMetrics,
                PreOperationValues = isPreOperation ? response.Attributes : null,
                PostOperationValues = isPostOperation ? response.Attributes : null,
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is InvalidOperationException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute UpdateItem operation. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a DeleteItem operation and removes the entity from DynamoDB (Primary API).
    /// This method populates DynamoDbOperationContext.Current with operation metadata including PreOperationValues.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The DeleteItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static async Task DeleteAsync<T>(
        this DeleteItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToDeleteItemRequest();
            var response = await builder.GetDynamoDbClient().DeleteItemAsync(request, cancellationToken);

            // Populate context with DeleteItemResponse metadata
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "DeleteItem",
                TableName = request.TableName,
                ConsumedCapacity = response.ConsumedCapacity,
                ItemCollectionMetrics = response.ItemCollectionMetrics,
                PreOperationValues = response.Attributes, // If ReturnValues was set to ALL_OLD
                ResponseMetadata = response.ResponseMetadata
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute DeleteItem operation. Error: {ex.Message}", ex);
        }
    }

}
