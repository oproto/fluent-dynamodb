namespace Oproto.FluentDynamoDb.Hydration;

/// <summary>
/// Registry for entity hydrators.
/// Provides a mechanism to register and retrieve hydrators for entity types
/// without using reflection.
/// </summary>
public interface IEntityHydratorRegistry
{
    /// <summary>
    /// Gets the hydrator for an entity type, or null if not registered.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The hydrator for the entity type, or null if not registered.</returns>
    IAsyncEntityHydrator<TEntity>? GetHydrator<TEntity>() where TEntity : class;
    
    /// <summary>
    /// Registers a hydrator for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="hydrator">The hydrator to register.</param>
    void Register<TEntity>(IAsyncEntityHydrator<TEntity> hydrator) where TEntity : class;
}
