using System.Collections.Concurrent;

namespace Oproto.FluentDynamoDb.Hydration;

/// <summary>
/// Default implementation of <see cref="IEntityHydratorRegistry"/>.
/// Uses a concurrent dictionary for thread-safe registration and lookup.
/// </summary>
public sealed class DefaultEntityHydratorRegistry : IEntityHydratorRegistry
{
    /// <summary>
    /// Gets the singleton instance of the default entity hydrator registry.
    /// </summary>
    public static readonly DefaultEntityHydratorRegistry Instance = new();
    
    private readonly ConcurrentDictionary<Type, object> _hydrators = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEntityHydratorRegistry"/> class.
    /// </summary>
    public DefaultEntityHydratorRegistry()
    {
    }
    
    /// <inheritdoc />
    public IAsyncEntityHydrator<TEntity>? GetHydrator<TEntity>() where TEntity : class
    {
        if (_hydrators.TryGetValue(typeof(TEntity), out var hydrator))
        {
            return (IAsyncEntityHydrator<TEntity>)hydrator;
        }
        return null;
    }
    
    /// <inheritdoc />
    public void Register<TEntity>(IAsyncEntityHydrator<TEntity> hydrator) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(hydrator);
        _hydrators[typeof(TEntity)] = hydrator;
    }
}
