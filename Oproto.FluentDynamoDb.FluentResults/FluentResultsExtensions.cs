using FluentResults;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

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
            var entity = await EnhancedExecuteAsyncExtensions.GetItemAsync<T>(builder, cancellationToken);
            return Result.Ok(entity);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<T?>($"Failed to execute GetItem operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
        }
    }

    /// <summary>
    /// Executes a GetItem operation with blob reference support and maps the result to a strongly-typed entity, returning a Result&lt;T?&gt;.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="builder">The GetItemRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for retrieving blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result containing the mapped entity (or null if not found) or error details.</returns>
    public static async Task<Result<T?>> GetItemAsyncResult<T>(
        this GetItemRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var entity = await EnhancedExecuteAsyncExtensions.GetItemAsync<T>(builder, blobProvider, cancellationToken);
            return Result.Ok(entity);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<T?>($"Failed to execute GetItem operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EnhancedExecuteAsyncExtensions.ToListAsync<T>(builder, cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute Query operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EnhancedExecuteAsyncExtensions.ToCompositeEntityListAsync<T>(builder, cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute Query operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        this QueryRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EnhancedExecuteAsyncExtensions.ToCompositeEntityAsync<T>(builder, cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute Query operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        this ScanRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EnhancedExecuteAsyncExtensions.ToListAsync<T>(builder, cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute Scan operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        this ScanRequestBuilder builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        try
        {
            var response = await EnhancedExecuteAsyncExtensions.ToCompositeEntityListAsync<T>(builder, cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute Scan operation for {typeof(T).Name}: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        where T : class
    {
        try
        {
            await EnhancedExecuteAsyncExtensions.PutAsync(builder, cancellationToken);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute PutItem operation: {ex.Message}")
                .WithError(new ExceptionalError(ex));
        }
    }

    /// <summary>
    /// Executes a PutItem operation with blob reference support and stores the entity in DynamoDB, returning a Result.
    /// This method uses the Primary API which populates DynamoDbOperationContext.Current with operation metadata.
    /// Use this overload when the entity has properties marked with [BlobReference] attribute.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The PutItemRequestBuilder instance.</param>
    /// <param name="blobProvider">The blob storage provider for storing blob references.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A Result indicating success or containing error details.</returns>
    public static async Task<Result> PutAsyncResult<T>(
        this PutItemRequestBuilder<T> builder,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            await EnhancedExecuteAsyncExtensions.PutAsync(builder, blobProvider, cancellationToken);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute PutItem operation: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        where T : class
    {
        try
        {
            await EnhancedExecuteAsyncExtensions.UpdateAsync(builder, cancellationToken);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute UpdateItem operation: {ex.Message}")
                .WithError(new ExceptionalError(ex));
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
        where T : class
    {
        try
        {
            await EnhancedExecuteAsyncExtensions.DeleteAsync(builder, cancellationToken);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to execute DeleteItem operation: {ex.Message}")
                .WithError(new ExceptionalError(ex));
        }
    }
}
