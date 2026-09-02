using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentResults;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// FluentResults extensions for enhanced ExecuteAsync methods.
/// These extensions wrap the enhanced ExecuteAsync methods to return Result&lt;T&gt; instead of throwing exceptions.
/// </summary>
public static class FluentResultsExtensions
{
    /// <summary>
    /// Executes a GetItem operation and maps the result to a strongly-typed entity, returning a Result&lt;T?&gt;.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the mapped entity (or null if not found) or error details.</returns>
    public static async Task<Result<T?>> GetItemAsyncResult<T>(
        this GetItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var entity = await EntityExecuteAsyncExtensions.GetItemAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(entity);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<T?>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a Query operation and maps each DynamoDB item to a separate entity instance (1:1 mapping), returning a Result&lt;T&gt;.
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the list of mapped entities or error details.</returns>
    public static async Task<Result<List<T>>> ToListAsyncResult<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EntityExecuteAsyncExtensions.ToListAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a Query operation and combines multiple DynamoDB items into composite entities (N:1 mapping), returning a Result&lt;T&gt;.
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the list of composite entities or error details.</returns>
    public static async Task<Result<List<T>>> ToCompositeEntityListAsyncResult<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EntityExecuteAsyncExtensions.ToCompositeEntityListAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a Query operation and returns a single composite entity (N:1 mapping), returning a Result&lt;T&gt;.
    /// Multiple DynamoDB items with the same partition key are combined into a single T instance.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The QueryRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the single composite entity or error details.</returns>
    public static async Task<Result<T?>> ToCompositeEntityAsyncResult<T>(
        this QueryRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EntityExecuteAsyncExtensions.ToCompositeEntityAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a Scan operation and maps each DynamoDB item to a separate entity instance (1:1 mapping), returning a Result&lt;T&gt;.
    /// Each DynamoDB item becomes a separate T instance in the returned list.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the list of mapped entities or error details.</returns>
    public static async Task<Result<List<T>>> ToListAsyncResult<T>(
        this ScanRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EntityExecuteAsyncExtensions.ToListAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a Scan operation and combines multiple DynamoDB items into composite entities (N:1 mapping), returning a Result&lt;T&gt;.
    /// Multiple DynamoDB items with the same partition key are combined into single T instances.
    /// Warning: Scan operations can be expensive on large tables. Use Query operations when possible.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The ScanRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the list of composite entities or error details.</returns>
    public static async Task<Result<List<T>>> ToCompositeEntityListAsyncResult<T>(
        this ScanRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EntityExecuteAsyncExtensions.ToCompositeEntityListAsync<T>(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a PutItem operation and stores the entity in DynamoDB, returning a Result.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// PutItem creates a new item or completely replaces an existing item with the same primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result indicating success or containing error details.</returns>
    public static async Task<Result> PutAsyncResult<T>(
        this PutItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            await EntityExecuteAsyncExtensions.PutAsync(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes an UpdateItem operation and modifies the entity in DynamoDB, returning a Result.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// UpdateItem modifies existing items or creates them if they don't exist (upsert behavior).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The UpdateItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result indicating success or containing error details.</returns>
    public static async Task<Result> UpdateAsyncResult<T>(
        this UpdateItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            await EntityExecuteAsyncExtensions.UpdateAsync(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a DeleteItem operation and removes the entity from DynamoDB, returning a Result.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The DeleteItemRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result indicating success or containing error details.</returns>
    public static async Task<Result> DeleteAsyncResult<T>(
        this DeleteItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            await EntityExecuteAsyncExtensions.DeleteAsync(builder, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    #region Batch Operation Extensions

    /// <summary>
    /// Executes a BatchGetItem operation and returns a Result containing the BatchGetResponse.
    /// When unprocessed keys exist, the result is successful but includes warnings.
    /// </summary>
    /// <param name="builder">The BatchGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the BatchGetResponse or error details.</returns>
    public static async Task<Result<BatchGetResponse>> ExecuteAsyncResult(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await builder.ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
            var result = Result.Ok(response);
            
            // Add warnings for unprocessed keys
            if (response.HasUnprocessedKeys)
            {
                var unprocessedCount = response.UnprocessedKeys.Values.Sum(ka => ka.Keys?.Count ?? 0);
                result.WithReason(new UnprocessedItemsWarning(
                    $"Batch get completed with {unprocessedCount} unprocessed keys across {response.UnprocessedKeys.Count} tables.",
                    unprocessedCount,
                    response.UnprocessedKeys.Keys.ToList()));
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<BatchGetResponse>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a BatchWriteItem operation and returns a Result containing the BatchWriteItemResponse.
    /// When unprocessed items exist, the result is successful but includes warnings.
    /// </summary>
    /// <param name="builder">The BatchWriteBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the BatchWriteItemResponse or error details.</returns>
    public static async Task<Result<BatchWriteItemResponse>> ExecuteAsyncResult(
        this BatchWriteBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await builder.ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
            var result = Result.Ok(response);
            
            // Add warnings for unprocessed items
            if (response.UnprocessedItems != null && response.UnprocessedItems.Count > 0)
            {
                var unprocessedCount = response.UnprocessedItems.Values.Sum(list => list.Count);
                result.WithReason(new UnprocessedItemsWarning(
                    $"Batch write completed with {unprocessedCount} unprocessed items across {response.UnprocessedItems.Count} tables.",
                    unprocessedCount,
                    response.UnprocessedItems.Keys.ToList()));
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<BatchWriteItemResponse>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a BatchExecuteStatement (PartiQL) operation and returns a Result containing the BatchPartiQLResponse.
    /// </summary>
    /// <param name="builder">The BatchPartiQLBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the BatchPartiQLResponse or error details.</returns>
    public static async Task<Result<BatchPartiQLResponse>> ExecuteAsyncResult(
        this BatchPartiQLBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await builder.ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
            var result = Result.Ok(response);
            
            // Add warnings for any statement errors
            if (response.HasAnyErrors)
            {
                var errors = response.GetAllErrors();
                foreach (var (index, error) in errors)
                {
                    result.WithReason(new BatchStatementErrorWarning(
                        $"Statement at index {index} failed: {error.Code} - {error.Message}",
                        index,
                        error.Code,
                        error.Message));
                }
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<BatchPartiQLResponse>(DynamoDbErrors.FromException(ex));
        }
    }

    #region BatchGetBuilder ExecuteAndMapAsyncResult Overloads

    /// <summary>
    /// Executes the batch and deserializes a single item, returning a Result.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <param name="builder">The BatchGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the deserialized entity (or null if missing) or error details.</returns>
    public static async Task<Result<T1?>> ExecuteAndMapAsyncResult<T1>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
    {
        try
        {
            var entity = await builder.ExecuteAndMapAsync<T1>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(entity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<T1?>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes two items, returning a Result.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <param name="builder">The BatchGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing a tuple of deserialized entities (nulls for missing items) or error details.</returns>
    public static async Task<Result<(T1?, T2?)>> ExecuteAndMapAsyncResult<T1, T2>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes three items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?)>> ExecuteAndMapAsyncResult<T1, T2, T3>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes four items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes five items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes six items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes seven items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?, T7?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6, T7>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?, T7?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the batch and deserializes eight items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6, T7, T8>(
        this BatchGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
        where T8 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7, T8>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?)>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #endregion

    #region Transaction Operation Extensions

    /// <summary>
    /// Executes a TransactWriteItems operation and returns a Result containing the TransactWriteItemsResponse.
    /// When a transaction is cancelled, the result contains a TransactionCancelledError with cancellation reasons.
    /// </summary>
    /// <param name="builder">The TransactionWriteBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the TransactWriteItemsResponse or error details.</returns>
    public static async Task<Result<TransactWriteItemsResponse>> ExecuteAsyncResult(
        this TransactionWriteBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await builder.ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TransactionCanceledException tce)
        {
            // Extract cancellation reasons and create a specific error
            return Result.Fail<TransactWriteItemsResponse>(DynamoDbErrors.FromException(tce));
        }
        catch (TransactionConflictException tcex)
        {
            return Result.Fail<TransactWriteItemsResponse>(DynamoDbErrors.FromException(tcex));
        }
        catch (IdempotentParameterMismatchException ipmex)
        {
            return Result.Fail<TransactWriteItemsResponse>(DynamoDbErrors.FromException(ipmex));
        }
        catch (Exception ex)
        {
            return Result.Fail<TransactWriteItemsResponse>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a TransactGetItems operation and returns a Result containing the TransactionGetResponse.
    /// </summary>
    /// <param name="builder">The TransactionGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the TransactionGetResponse or error details.</returns>
    public static async Task<Result<TransactionGetResponse>> ExecuteAsyncResult(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await builder.ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TransactionCanceledException tce)
        {
            // Extract cancellation reasons and create a specific error
            return Result.Fail<TransactionGetResponse>(DynamoDbErrors.FromException(tce));
        }
        catch (Exception ex)
        {
            return Result.Fail<TransactionGetResponse>(DynamoDbErrors.FromException(ex));
        }
    }

    #region TransactionGetBuilder ExecuteAndMapAsyncResult Overloads

    /// <summary>
    /// Executes the transaction and deserializes a single item, returning a Result.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <param name="builder">The TransactionGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the deserialized entity (or null if missing) or error details.</returns>
    public static async Task<Result<T1?>> ExecuteAndMapAsyncResult<T1>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
    {
        try
        {
            var entity = await builder.ExecuteAndMapAsync<T1>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(entity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<T1?>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes two items, returning a Result.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <param name="builder">The TransactionGetBuilder instance.</param>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing a tuple of deserialized entities (nulls for missing items) or error details.</returns>
    public static async Task<Result<(T1?, T2?)>> ExecuteAndMapAsyncResult<T1, T2>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes three items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?)>> ExecuteAndMapAsyncResult<T1, T2, T3>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes four items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes five items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes six items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes seven items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?, T7?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6, T7>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?, T7?)>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes eight items, returning a Result.
    /// </summary>
    public static async Task<Result<(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?)>> ExecuteAndMapAsyncResult<T1, T2, T3, T4, T5, T6, T7, T8>(
        this TransactionGetBuilder builder,
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
        where T8 : class, IDynamoDbEntity
    {
        try
        {
            var result = await builder.ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7, T8>(client, cancellationToken).ConfigureAwait(false);
            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?)>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #endregion

    #region PartiQL Operation Extensions

    /// <summary>
    /// Executes a PartiQL SELECT query and returns hydrated entities as a list, returning a Result&lt;List&lt;T&gt;&gt;.
    /// This method wraps ToListAsync with try/catch and returns a Result instead of throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The PartiQLRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the list of hydrated entities or error details.</returns>
    /// <example>
    /// <code>
    /// var result = await table.ExecutePartiQL&lt;User&gt;(
    ///     "SELECT * FROM Users WHERE pk = ?", userId)
    ///     .ToListAsyncResult();
    /// 
    /// if (result.IsSuccess)
    /// {
    ///     foreach (var user in result.Value)
    ///         Console.WriteLine(user.Name);
    /// }
    /// else
    /// {
    ///     Console.WriteLine($"Error: {result.Errors.First().Message}");
    /// }
    /// </code>
    /// </example>
    public static async Task<Result<List<T>>> ToListAsyncResult<T>(
        this PartiQLRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var entities = await builder.ToListAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok(entities);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<List<T>>(DynamoDbErrors.FromException(ex));
        }
    }

    /// <summary>
    /// Executes a PartiQL non-SELECT statement (INSERT, UPDATE, DELETE) and returns a Result.
    /// This method wraps ExecuteAsync with try/catch and returns a Result instead of throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The PartiQLRequestBuilder instance.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result indicating success or containing error details.</returns>
    /// <example>
    /// <code>
    /// var result = await table.ExecutePartiQL&lt;User&gt;(
    ///     "UPDATE Users SET name = ? WHERE pk = ?", "Jane Doe", userId)
    ///     .ExecuteAsyncResult();
    /// 
    /// if (result.IsFailed)
    /// {
    ///     Console.WriteLine($"Update failed: {result.Errors.First().Message}");
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> ExecuteAsyncResult<T>(
        this PartiQLRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion
}
