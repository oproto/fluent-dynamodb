using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Concurrent;

namespace Oproto.FluentDynamoDb.SourceGenerator.Performance;

/// <summary>
/// High-performance cache for entity metadata to improve source generator performance.
/// Uses weak references and automatic cleanup to prevent memory leaks.
/// </summary>
internal static class EntityMetadataCache
{
    private static readonly ConcurrentDictionary<string, WeakReference<CachedEntityMetadata>> _cache = new();
    private static readonly object _cleanupLock = new object();
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static int _hits = 0;
    private static int _misses = 0;

    /// <summary>
    /// Gets or creates entity metadata with caching for improved performance.
    /// </summary>
    public static CachedEntityMetadata GetOrCreate(EntityModel entity)
    {
        var key = $"{entity.Namespace}.{entity.ClassName}";

        // Try to get from cache first
        if (_cache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var cachedMetadata))
        {
            Interlocked.Increment(ref _hits);
            return cachedMetadata;
        }

        // Cache miss - create new metadata
        Interlocked.Increment(ref _misses);
        var metadata = CreateEntityMetadata(entity);

        // Store in cache with weak reference
        _cache.AddOrUpdate(key, new WeakReference<CachedEntityMetadata>(metadata), (_, _) => new WeakReference<CachedEntityMetadata>(metadata));

        // Periodic cleanup
        PerformPeriodicCleanup();

        return metadata;
    }

    /// <summary>
    /// Creates entity metadata from entity model.
    /// </summary>
    private static CachedEntityMetadata CreateEntityMetadata(EntityModel entity)
    {
        return new CachedEntityMetadata
        {
            TableName = entity.TableName,
            Properties = entity.Properties.Select(p => new CachedPropertyMetadata
            {
                PropertyName = p.PropertyName,
                AttributeName = p.AttributeName,
                PropertyType = p.PropertyType,
                IsPartitionKey = p.IsPartitionKey,
                IsSortKey = p.IsSortKey,
                SupportedOperations = p.Queryable != null ? p.Queryable.SupportedOperations : Array.Empty<DynamoDbOperation>(),
                AvailableInIndexes = p.Queryable != null ? p.Queryable.AvailableInIndexes : Array.Empty<string>()
            }).ToArray(),
            Indexes = entity.Indexes.Select(i => new CachedIndexMetadata
            {
                IndexName = i.IndexName,
                PartitionKeyProperty = i.PartitionKeyProperty,
                SortKeyProperty = i.SortKeyProperty,
                ProjectedProperties = i.ProjectedProperties
            }).ToArray(),
            Relationships = entity.Relationships.Select(r => new CachedRelationshipMetadata
            {
                PropertyName = r.PropertyName,
                SortKeyPattern = r.SortKeyPattern,
                EntityType = r.EntityType,
                IsCollection = r.IsCollection
            }).ToArray()
        };
    }

    /// <summary>
    /// Gets cache statistics for monitoring performance.
    /// </summary>
    public static CacheStatistics GetStatistics()
    {
        var totalRequests = _hits + _misses;
        var hitRate = totalRequests > 0 ? (double)_hits / totalRequests : 0.0;

        var aliveEntries = 0;
        var deadEntries = 0;

        foreach (var kvp in _cache)
        {
            if (kvp.Value.TryGetTarget(out _))
                aliveEntries++;
            else
                deadEntries++;
        }

        var totalEntries = aliveEntries + deadEntries;
        var memoryEfficiency = totalEntries > 0 ? (double)aliveEntries / totalEntries : 1.0;

        return new CacheStatistics
        {
            TotalEntries = totalEntries,
            AliveEntries = aliveEntries,
            DeadEntries = deadEntries,
            HitRate = hitRate,
            MemoryEfficiency = memoryEfficiency,
            CleanupRecommended = deadEntries > 10 || memoryEfficiency < 0.7
        };
    }

    /// <summary>
    /// Clears the cache completely.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
        _hits = 0;
        _misses = 0;
        _lastCleanup = DateTime.UtcNow;
    }

    /// <summary>
    /// Performs periodic cleanup of dead weak references.
    /// </summary>
    private static void PerformPeriodicCleanup()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(5))
            return;

        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(5))
                return;

            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            _lastCleanup = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Cache statistics for monitoring and optimization.
/// </summary>
internal class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int AliveEntries { get; set; }
    public int DeadEntries { get; set; }
    public double HitRate { get; set; }
    public double MemoryEfficiency { get; set; }
    public bool CleanupRecommended { get; set; }
}

/// <summary>
/// Cached entity metadata for source generator use.
/// </summary>
internal class CachedEntityMetadata
{
    public string TableName { get; set; } = string.Empty;
    public CachedPropertyMetadata[] Properties { get; set; } = Array.Empty<CachedPropertyMetadata>();
    public CachedIndexMetadata[] Indexes { get; set; } = Array.Empty<CachedIndexMetadata>();
    public CachedRelationshipMetadata[] Relationships { get; set; } = Array.Empty<CachedRelationshipMetadata>();
}

/// <summary>
/// Cached property metadata for source generator use.
/// </summary>
internal class CachedPropertyMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();
    public string[] AvailableInIndexes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Cached index metadata for source generator use.
/// </summary>
internal class CachedIndexMetadata
{
    public string IndexName { get; set; } = string.Empty;
    public string PartitionKeyProperty { get; set; } = string.Empty;
    public string SortKeyProperty { get; set; }
    public string[] ProjectedProperties { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Cached relationship metadata for source generator use.
/// </summary>
internal class CachedRelationshipMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string SortKeyPattern { get; set; } = string.Empty;
    public string EntityType { get; set; }
    public bool IsCollection { get; set; }
}