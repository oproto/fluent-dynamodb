using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Enhanced ExecuteAsync extensions that provide strongly-typed entity mapping.
/// These extensions work with entities that implement IDynamoDbEntity interface.
/// </summary>
public static class EnhancedExecuteAsyncExtensions
{
    /// <summary>
    /// Executes a GetItem operation and maps the result to a strongly-typed entity.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A GetItemResponse containing the mapped entity or null if not found.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<GetItemResponse<T>> ExecuteAsync<T>(
        this GetItemRequestBuilder builder, 
        CancellationToken cancellationToken = default) 
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await builder.ExecuteAsync(cancellationToken);
            
            return new GetItemResponse<T>
            {
                Item = response.Item != null && T.MatchesEntity(response.Item) 
                    ? T.FromDynamoDb<T>(response.Item) 
                    : null,
                ConsumedCapacity = response.ConsumedCapacity,
                ResponseMetadata = response.ResponseMetadata
            };
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute GetItem operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Query operation and maps the results to strongly-typed entities.
    /// For multi-item entities, items with the same partition key are automatically grouped.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A QueryResponse containing the mapped entities.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<QueryResponse<T>> ExecuteAsync<T>(
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await builder.ExecuteAsync(cancellationToken);
            
            // Filter items that match the entity type
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();
            
            // Group items by partition key for multi-item entities
            var entityItems = matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1 
                    ? T.FromDynamoDb<T>(group.First()) 
                    : T.FromDynamoDb<T>(group.ToList()))
                .ToList();
            
            return new QueryResponse<T>
            {
                Items = entityItems,
                LastEvaluatedKey = response.LastEvaluatedKey,
                ConsumedCapacity = response.ConsumedCapacity,
                Count = entityItems.Count,
                ScannedCount = response.ScannedCount ?? 0,
                ResponseMetadata = response.ResponseMetadata
            };
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
    public static PutItemRequestBuilder WithItem<T>(
        this PutItemRequestBuilder builder, 
        T item) 
        where T : class, IDynamoDbEntity
    {
        try
        {
            var attributeDict = T.ToDynamoDb(item);
            return builder.WithItem(attributeDict);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert {typeof(T).Name} entity to DynamoDB format. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a Scan operation and maps the results to strongly-typed entities.
    /// For multi-item entities, items with the same partition key are automatically grouped.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A ScanResponse containing the mapped entities.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity mapping fails.</exception>
    public static async Task<ScanResponse<T>> ExecuteAsync<T>(
        this ScanRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await builder.ExecuteAsync(cancellationToken);
            
            // Filter items that match the entity type
            var matchingItems = response.Items.Where(T.MatchesEntity).ToList();
            
            // Group items by partition key for multi-item entities
            var entityItems = matchingItems
                .GroupBy(T.GetPartitionKey)
                .Select(group => group.Count() == 1 
                    ? T.FromDynamoDb<T>(group.First()) 
                    : T.FromDynamoDb<T>(group.ToList()))
                .ToList();
            
            return new ScanResponse<T>
            {
                Items = entityItems,
                LastEvaluatedKey = response.LastEvaluatedKey,
                ConsumedCapacity = response.ConsumedCapacity,
                Count = entityItems.Count,
                ScannedCount = response.ScannedCount ?? 0,
                ResponseMetadata = response.ResponseMetadata
            };
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            throw new DynamoDbMappingException(
                $"Failed to execute Scan operation and map to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all DynamoDB items for a multi-item entity.
    /// This is useful for batch operations or when you need to work with individual items.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <returns>A list of DynamoDB items representing the entity.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity conversion fails.</exception>
    public static List<Dictionary<string, AttributeValue>> GetDynamoDbItems<T>(T entity) 
        where T : class, IDynamoDbEntity
    {
        try
        {
            return T.ToDynamoDbMultiple(entity);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to convert {typeof(T).Name} entity to multiple DynamoDB items. Error: {ex.Message}", ex);
        }
    }
}