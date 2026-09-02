using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Mapping;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Entity ExecuteAsync extensions that provide strongly-typed entity mapping.
/// These extensions work with entities that implement IDynamoDbEntity interface.
/// </summary>
public static class EntityExecuteAsyncExtensions
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
            var response = await builder.GetDynamoDbClient().GetItemAsync(request, cancellationToken).ConfigureAwait(false);

            // Populate builder.Response with response metadata for direct access
            builder.Response = new GetItemOperationResponse
            {
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata
            };

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

            // Check if a hydrator is registered for async deserialization (encrypted/blob entities)
            var options = builder.GetOptions();
            var hydrator = options.HydratorRegistry?.GetHydrator<T>();
            if (hydrator != null)
            {
                var blobProvider = options.BlobStorageProvider;
                return await hydrator.HydrateAsync(response.Item, blobProvider, options, cancellationToken).ConfigureAwait(false);
            }

            return T.FromDynamoDb<T>(response.Item, options);
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
    /// It also populates builder.Response with response metadata for direct access.
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
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken).ConfigureAwait(false);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate builder.Response with response metadata for direct access
            builder.Response = new QueryOperationResponse
            {
                LastEvaluatedKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null,
                ScannedCount = response.ScannedCount,
                ResultCount = response.Count,
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata
            };

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
            var matchingItems = items.Where(T.MatchesEntity).ToList();
            
            // Check if a hydrator is registered for async deserialization (encrypted/blob entities)
            var hydrator = options.HydratorRegistry?.GetHydrator<T>();
            if (hydrator != null)
            {
                var blobProvider = options.BlobStorageProvider;
                var results = new List<T>(matchingItems.Count);
                foreach (var item in matchingItems)
                {
                    results.Add(await hydrator.HydrateAsync(item, blobProvider, options, cancellationToken).ConfigureAwait(false));
                }
                return results;
            }
            
            var entityItems = matchingItems
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
    /// Executes a Query operation and combines multiple DynamoDB items into composite entities (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Primary entity is identified by sort key patterns, related entities populate properties using [RelatedEntity] attributes.
    /// Use this method when you want to work with composite entities that span multiple DynamoDB items.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Pagination Limitation:</strong> This method executes a single DynamoDB Query operation and does not
    /// handle pagination. All items for each composite entity must be returned in a single response (up to 1MB).
    /// If your composite entities span more items than can fit in a single response, consider:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Using manual pagination with <see cref="QueryRequestBuilder{T}.WithExclusiveStartKey"/> and checking <c>builder.Response.LastEvaluatedKey</c></description></item>
    /// <item><description>Designing smaller composite entities with fewer related items</description></item>
    /// <item><description>Using <see cref="ToListAsync{T}(QueryRequestBuilder{T}, CancellationToken)"/> for individual item retrieval with manual assembly</description></item>
    /// </list>
    /// <para>
    /// Each page of results is processed independently - composite entities are assembled only from items
    /// within the same response page.
    /// </para>
    /// </remarks>
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
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken).ConfigureAwait(false);
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
            
            // Check if a hydrator is registered for async deserialization (encrypted/blob entities)
            var hydrator = options.HydratorRegistry?.GetHydrator<T>();
            if (hydrator != null)
            {
                var blobProvider = options.BlobStorageProvider;
                var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();
                var results = new List<T>(groups.Count);
                foreach (var group in groups)
                {
                    var groupItems = group.ToList();
                    results.Add(groupItems.Count == 1
                        ? await hydrator.HydrateAsync(groupItems[0], blobProvider, options, cancellationToken).ConfigureAwait(false)
                        : await hydrator.HydrateAsync(groupItems, blobProvider, options, cancellationToken).ConfigureAwait(false));
                }
                return results;
            }
            
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
    /// Executes a Query operation and returns a single composite entity (N:1 mapping).
    /// Multiple DynamoDB items with the same partition key are combined into a single T instance.
    /// Primary entity is identified by sort key patterns, related entities populate properties using [RelatedEntity] attributes.
    /// Use this method when you expect to get a single composite entity from the query.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Pagination Limitation:</strong> This method executes a single DynamoDB Query operation and does not
    /// handle pagination. All items for the composite entity must be returned in a single response (up to 1MB).
    /// If your composite entity spans more items than can fit in a single response, the related entity collections
    /// will be incomplete.
    /// </para>
    /// <para>
    /// For composite entities that may exceed the 1MB response limit, consider:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Using manual pagination with <see cref="QueryRequestBuilder{T}.WithExclusiveStartKey"/> and checking <c>builder.Response.LastEvaluatedKey</c></description></item>
    /// <item><description>Designing smaller composite entities with fewer related items</description></item>
    /// <item><description>Using <see cref="ToListAsync{T}(QueryRequestBuilder{T}, CancellationToken)"/> for individual item retrieval with manual assembly</description></item>
    /// </list>
    /// </remarks>
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
            var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken).ConfigureAwait(false);
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

            // For composite entities, pass all items to FromDynamoDb
            // The multi-item FromDynamoDb method handles identifying the primary entity
            // and related entities based on sort key patterns
            // We don't filter by MatchesEntity here because related entities (e.g., InvoiceLine)
            // won't match the primary entity type (e.g., Invoice)
            if (items.Count == 0)
                return null;

            var options = builder.GetOptions();

            // Always use async composite assembly path — this ensures both encrypted and
            // non-encrypted parent entities go through the same full composite assembly logic
            // (primary entity identification, related entity pattern matching, collection population)
            return await T.FromDynamoDbAsync<T>(items, options?.BlobStorageProvider, options?.FieldEncryptor, options, cancellationToken).ConfigureAwait(false);
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
    /// For entities with encrypted or blob storage properties, serialization is deferred
    /// to async execution time via the builder's hydrator-aware WithItem instance method.
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
        // Delegate to the builder's instance method which handles:
        // - Hydrator-based deferred serialization for encrypted/blob entities
        // - NotSupportedException fallback for entities without registered hydrators
        // - Direct synchronous serialization for standard entities
        return builder.WithItem(item);
    }

    /// <summary>
    /// Executes a Scan operation and maps each DynamoDB item to a separate entity instance (1:1 mapping).
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// This method populates DynamoDbOperationContext.Current with operation metadata.
    /// It also populates builder.Response with response metadata for direct access.
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
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken).ConfigureAwait(false);
            var items = response.Items ?? new List<Dictionary<string, AttributeValue>>();

            // Populate builder.Response with response metadata for direct access
            builder.Response = new ScanOperationResponse
            {
                LastEvaluatedKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null,
                ScannedCount = response.ScannedCount,
                ResultCount = response.Count,
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata
            };

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
            var matchingItems = items.Where(T.MatchesEntity).ToList();
            
            // Check if a hydrator is registered for async deserialization (encrypted/blob entities)
            var hydrator = options.HydratorRegistry?.GetHydrator<T>();
            if (hydrator != null)
            {
                var blobProvider = options.BlobStorageProvider;
                var results = new List<T>(matchingItems.Count);
                foreach (var item in matchingItems)
                {
                    results.Add(await hydrator.HydrateAsync(item, blobProvider, options, cancellationToken).ConfigureAwait(false));
                }
                return results;
            }
            
            var entityItems = matchingItems
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
            var response = await builder.GetDynamoDbClient().ScanAsync(request, cancellationToken).ConfigureAwait(false);
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
            
            // Check if a hydrator is registered for async deserialization (encrypted/blob entities)
            var hydrator = options.HydratorRegistry?.GetHydrator<T>();
            if (hydrator != null)
            {
                var blobProvider = options.BlobStorageProvider;
                var groups = matchingItems.GroupBy(T.GetPartitionKey).ToList();
                var results = new List<T>(groups.Count);
                foreach (var group in groups)
                {
                    var groupItems = group.ToList();
                    results.Add(groupItems.Count == 1
                        ? await hydrator.HydrateAsync(groupItems[0], blobProvider, options, cancellationToken).ConfigureAwait(false)
                        : await hydrator.HydrateAsync(groupItems, blobProvider, options, cancellationToken).ConfigureAwait(false));
                }
                return results;
            }
            
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
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Resolve deferred async serialization for encrypted entities before building the request
            if (builder.HasDeferredEntity)
            {
                var options = builder.GetOptions();
                var hydrator = options.HydratorRegistry?.GetHydrator<T>();
                if (hydrator != null)
                {
                    var entity = builder.GetDeferredEntity()!;
                    var blobProvider = options.BlobStorageProvider;
                    var item = await hydrator.SerializeAsync(entity, blobProvider, options, cancellationToken: cancellationToken).ConfigureAwait(false);
                    builder.SetResolvedItem(item);
                }
            }

            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToPutItemRequest();
            var response = await builder.GetDynamoDbClient().PutItemAsync(request, cancellationToken).ConfigureAwait(false);

            // Populate builder.Response with response metadata for direct access
            builder.Response = new PutItemOperationResponse
            {
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata,
                ItemCollectionMetrics = response.ItemCollectionMetrics
            };

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
    /// It also populates builder.Response with response metadata for direct access.
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
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Use ToDynamoDbResponseAsync which handles encryption and execution
            var response = await builder.ToDynamoDbResponseAsync(cancellationToken).ConfigureAwait(false);
            
            // Get the request after encryption to check ReturnValues setting
            var request = builder.ToUpdateItemRequest();

            // Populate builder.Response with response metadata for direct access
            builder.Response = new UpdateItemOperationResponse
            {
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata,
                ItemCollectionMetrics = response.ItemCollectionMetrics
            };

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
    /// It also populates builder.Response with response metadata for direct access.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The DeleteItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static async Task DeleteAsync<T>(
        this DeleteItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            // Call AWS SDK directly instead of builder's ExecuteAsync
            var request = builder.ToDeleteItemRequest();
            var response = await builder.GetDynamoDbClient().DeleteItemAsync(request, cancellationToken).ConfigureAwait(false);

            // Populate builder.Response with response metadata for direct access
            builder.Response = new DeleteItemOperationResponse
            {
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata,
                ItemCollectionMetrics = response.ItemCollectionMetrics
            };

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
