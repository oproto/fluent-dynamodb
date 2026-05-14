using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Mapping;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Response wrapper for batch PartiQL operations.
/// Provides typed access to SELECT results.
/// </summary>
/// <remarks>
/// <para>
/// BatchExecuteStatement returns one response per statement. For SELECT statements,
/// the response contains the item (if found). For INSERT/UPDATE/DELETE statements,
/// the response may be empty or contain the affected item depending on the statement.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var response = await DynamoDbBatch.PartiQL
///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
///     .Add(table.ExecutePartiQL&lt;Order&gt;("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
///     .ExecuteAsync();
/// 
/// var user = response.GetItem&lt;User&gt;(0);
/// var order = response.GetItem&lt;Order&gt;(1);
/// </code>
/// </example>
public class BatchPartiQLResponse
{
    private readonly BatchExecuteStatementResponse _response;
    private readonly FluentDynamoDbOptions _options;

    /// <summary>
    /// Initializes a new instance of the BatchPartiQLResponse class.
    /// </summary>
    /// <param name="response">The underlying AWS SDK response.</param>
    /// <param name="options">Configuration options for entity deserialization.</param>
    internal BatchPartiQLResponse(BatchExecuteStatementResponse response, FluentDynamoDbOptions? options)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _options = options ?? new FluentDynamoDbOptions();
    }

    /// <summary>
    /// Gets the raw SDK response.
    /// </summary>
    public BatchExecuteStatementResponse RawResponse => _response;

    /// <summary>
    /// Gets the number of responses in the batch.
    /// </summary>
    public int Count => _response.Responses?.Count ?? 0;

    /// <summary>
    /// Gets a hydrated entity from a SELECT result at the specified index.
    /// Returns null for non-SELECT statements or if no item was returned.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to hydrate.</typeparam>
    /// <param name="index">The zero-based index of the statement in the batch.</param>
    /// <returns>The hydrated entity, or null if not found or not a SELECT result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when deserialization fails.</exception>
    /// <example>
    /// <code>
    /// var user = response.GetItem&lt;User&gt;(0);
    /// if (user != null)
    /// {
    ///     Console.WriteLine($"User: {user.Name}");
    /// }
    /// </code>
    /// </example>
    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        if (_response.Responses == null || _response.Responses.Count == 0)
            return null;

        if (index < 0 || index >= _response.Responses.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Index {index} is out of range. Response contains {_response.Responses.Count} items.");
        }

        var statementResponse = _response.Responses[index];

        // Check for errors in this statement's response
        if (statementResponse.Error != null)
        {
            throw new DynamoDbMappingException(
                $"Statement at index {index} failed: {statementResponse.Error.Code} - {statementResponse.Error.Message}");
        }

        var item = statementResponse.Item;
        if (item == null || item.Count == 0)
            return null;

        try
        {
            return TEntity.FromDynamoDb<TEntity>(item, _options);
        }
        catch (DynamoDbMappingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize item at index {index} to type {typeof(TEntity).Name}.",
                typeof(TEntity),
                MappingOperation.FromDynamoDb,
                item,
                innerException: ex)
                .WithContext("Index", index);
        }
    }

    /// <summary>
    /// Gets all items from a SELECT result at the specified index as a list.
    /// Note: BatchExecuteStatement returns one item per statement.
    /// For multi-row results, use single ExecutePartiQL().ToListAsync().
    /// </summary>
    /// <typeparam name="TEntity">The entity type to hydrate.</typeparam>
    /// <param name="index">The zero-based index of the statement in the batch.</param>
    /// <returns>A list containing the hydrated entity, or an empty list if not found.</returns>
    /// <example>
    /// <code>
    /// var users = response.GetItems&lt;User&gt;(0);
    /// </code>
    /// </example>
    public List<TEntity> GetItems<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        var item = GetItem<TEntity>(index);
        return item != null ? new List<TEntity> { item } : new List<TEntity>();
    }

    /// <summary>
    /// Checks if the statement at the specified index has an error.
    /// </summary>
    /// <param name="index">The zero-based index of the statement in the batch.</param>
    /// <returns>True if the statement has an error, false otherwise.</returns>
    public bool HasError(int index)
    {
        if (_response.Responses == null || index < 0 || index >= _response.Responses.Count)
            return false;

        return _response.Responses[index].Error != null;
    }

    /// <summary>
    /// Gets the error for the statement at the specified index, if any.
    /// </summary>
    /// <param name="index">The zero-based index of the statement in the batch.</param>
    /// <returns>The error, or null if no error occurred.</returns>
    public BatchStatementError? GetError(int index)
    {
        if (_response.Responses == null || index < 0 || index >= _response.Responses.Count)
            return null;

        return _response.Responses[index].Error;
    }

    /// <summary>
    /// Gets all errors from the batch response.
    /// </summary>
    /// <returns>A list of tuples containing the index and error for each failed statement.</returns>
    public List<(int Index, BatchStatementError Error)> GetAllErrors()
    {
        var errors = new List<(int Index, BatchStatementError Error)>();

        if (_response.Responses == null)
            return errors;

        for (int i = 0; i < _response.Responses.Count; i++)
        {
            var error = _response.Responses[i].Error;
            if (error != null)
            {
                errors.Add((i, error));
            }
        }

        return errors;
    }

    /// <summary>
    /// Indicates whether any statements in the batch have errors.
    /// </summary>
    public bool HasAnyErrors => _response.Responses?.Any(r => r.Error != null) ?? false;
}
