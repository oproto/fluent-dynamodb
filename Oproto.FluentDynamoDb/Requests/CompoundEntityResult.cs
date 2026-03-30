using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Result wrapper for queries that return multiple entity types from a compound entity table.
/// Provides type-safe access to entities filtered by their discriminator.
/// </summary>
/// <remarks>
/// <para>
/// Compound entity tables store multiple entity types in the same table, typically using
/// a discriminator attribute to distinguish between types. This class allows filtering
/// the results by entity type using the entity's MatchesEntity method.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await table.ExecutePartiQL&lt;Order&gt;(
///     "SELECT * FROM Orders WHERE pk = {0}",
///     "ORDER#456")
///     .ToCompoundEntityAsync();
/// 
/// var orders = result.GetEntities&lt;Order&gt;();
/// var orderLines = result.GetEntities&lt;OrderLine&gt;();
/// </code>
/// </example>
public class CompoundEntityResult
{
    private readonly List<Dictionary<string, AttributeValue>> _items;
    private readonly FluentDynamoDbOptions _options;

    /// <summary>
    /// Initializes a new instance of the CompoundEntityResult class.
    /// </summary>
    /// <param name="items">The raw DynamoDB items from the query response.</param>
    /// <param name="options">Configuration options for entity hydration.</param>
    internal CompoundEntityResult(List<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options)
    {
        _items = items ?? new List<Dictionary<string, AttributeValue>>();
        _options = options ?? new FluentDynamoDbOptions();
    }

    /// <summary>
    /// Gets the raw DynamoDB items from the query response.
    /// </summary>
    public IReadOnlyList<Dictionary<string, AttributeValue>> RawItems => _items;

    /// <summary>
    /// Gets the total number of items in the result.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets all entities of the specified type from the result.
    /// Uses the entity's MatchesEntity method to filter items.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to filter and hydrate.</typeparam>
    /// <returns>A list of hydrated entities of the specified type.</returns>
    /// <example>
    /// <code>
    /// var orders = result.GetEntities&lt;Order&gt;();
    /// var orderLines = result.GetEntities&lt;OrderLine&gt;();
    /// </code>
    /// </example>
    public List<TEntity> GetEntities<TEntity>() where TEntity : class, IDynamoDbEntity
    {
        return _items
            .Where(TEntity.MatchesEntity)
            .Select(item => TEntity.FromDynamoDb<TEntity>(item, _options))
            .ToList();
    }

    /// <summary>
    /// Gets the first entity of the specified type from the result, or null if not found.
    /// Uses the entity's MatchesEntity method to filter items.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to filter and hydrate.</typeparam>
    /// <returns>The first hydrated entity of the specified type, or null if not found.</returns>
    /// <example>
    /// <code>
    /// var order = result.GetFirstEntity&lt;Order&gt;();
    /// </code>
    /// </example>
    public TEntity? GetFirstEntity<TEntity>() where TEntity : class, IDynamoDbEntity
    {
        var item = _items.FirstOrDefault(TEntity.MatchesEntity);
        return item != null ? TEntity.FromDynamoDb<TEntity>(item, _options) : null;
    }

    /// <summary>
    /// Gets the count of entities of the specified type in the result.
    /// Uses the entity's MatchesEntity method to filter items.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to count.</typeparam>
    /// <returns>The number of items matching the specified entity type.</returns>
    /// <example>
    /// <code>
    /// var orderCount = result.GetEntityCount&lt;Order&gt;();
    /// var lineCount = result.GetEntityCount&lt;OrderLine&gt;();
    /// </code>
    /// </example>
    public int GetEntityCount<TEntity>() where TEntity : class, IDynamoDbEntity
    {
        return _items.Count(TEntity.MatchesEntity);
    }
}
