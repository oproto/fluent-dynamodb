using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Concurrent;

namespace Oproto.FluentDynamoDb.SourceGenerator.Performance;

/// <summary>
/// Incremental source generator with caching and performance optimizations.
/// Provides transform caching and efficient change detection.
/// </summary>
internal static class IncrementalSourceGenerator
{
    /// <summary>
    /// Transforms entity class with caching for improved performance.
    /// </summary>
    public static (EntityModel? EntityModel, string CacheKey) TransformEntityClass(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Generate cache key based on class content
        var cacheKey = GenerateCacheKey(classDecl, semanticModel);

        // Try to get from cache first
        if (EntityTransformCache.TryGetCached(cacheKey, out var cachedResult))
        {
            return (cachedResult, cacheKey);
        }

        // Cache miss - perform transformation
        try
        {
            var analyzer = new EntityAnalyzer();
            var entityModel = analyzer.AnalyzeEntity(classDecl, semanticModel);

            // Cache the result
            if (entityModel != null)
            {
                EntityTransformCache.Cache(cacheKey, entityModel);
            }

            return (entityModel, cacheKey);
        }
        catch (Exception)
        {
            // Return null on error
            return (null, cacheKey);
        }
    }

    /// <summary>
    /// Generates a cache key based on class declaration and semantic information.
    /// </summary>
    private static string GenerateCacheKey(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        var className = classDecl.Identifier.ValueText;
        var namespaceName = GetNamespace(classDecl);

        // Include attribute information in cache key
        var attributeInfo = string.Join("|",
            classDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(a => a.Name.ToString()));

        // Include property information in cache key
        var propertyInfo = string.Join("|",
            classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .Select(p => $"{p.Identifier.ValueText}:{p.Type}"));

        return $"{namespaceName}.{className}#{attributeInfo}#{propertyInfo}".GetHashCode().ToString();
    }

    /// <summary>
    /// Gets the namespace for a class declaration.
    /// </summary>
    private static string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        var fileScopedNamespace = classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNamespace != null)
        {
            return fileScopedNamespace.Name.ToString();
        }

        return "Global";
    }
}

/// <summary>
/// Cache for entity transformation results to improve incremental generation performance.
/// </summary>
internal static class EntityTransformCache
{
    private static readonly ConcurrentDictionary<string, WeakReference<EntityModel>> _cache = new();
    private static readonly object _maintenanceLock = new object();
    private static DateTime _lastMaintenance = DateTime.UtcNow;
    private static int _hits = 0;
    private static int _misses = 0;

    /// <summary>
    /// Tries to get a cached entity model.
    /// </summary>
    public static bool TryGetCached(string cacheKey, out EntityModel? entityModel)
    {
        if (_cache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out entityModel))
        {
            Interlocked.Increment(ref _hits);
            return true;
        }

        Interlocked.Increment(ref _misses);
        entityModel = null;
        return false;
    }

    /// <summary>
    /// Caches an entity model.
    /// </summary>
    public static void Cache(string cacheKey, EntityModel entityModel)
    {
        _cache.AddOrUpdate(cacheKey, new WeakReference<EntityModel>(entityModel), (_, _) => new WeakReference<EntityModel>(entityModel));

        // Periodic maintenance
        PerformPeriodicMaintenance();
    }

    /// <summary>
    /// Gets the cache hit rate for monitoring.
    /// </summary>
    public static double GetCacheHitRate()
    {
        var totalRequests = _hits + _misses;
        return totalRequests > 0 ? (double)_hits / totalRequests : 0.0;
    }

    /// <summary>
    /// Performs maintenance to clean up dead weak references.
    /// </summary>
    public static void PerformMaintenance()
    {
        lock (_maintenanceLock)
        {
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

            _lastMaintenance = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Performs periodic maintenance if needed.
    /// </summary>
    private static void PerformPeriodicMaintenance()
    {
        if (DateTime.UtcNow - _lastMaintenance > TimeSpan.FromMinutes(10))
        {
            PerformMaintenance();
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
        _hits = 0;
        _misses = 0;
        _lastMaintenance = DateTime.UtcNow;
    }
}