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
    /// Transforms entity type (class or record) with caching for improved performance.
    /// </summary>
    public static (EntityModel? EntityModel, string CacheKey) TransformEntityClass(GeneratorSyntaxContext context)
    {
        // Support both class and record declarations
        TypeDeclarationSyntax? typeDecl = context.Node switch
        {
            ClassDeclarationSyntax classDecl => classDecl,
            RecordDeclarationSyntax recordDecl => recordDecl,
            _ => null
        };

        if (typeDecl == null)
            return (null, string.Empty);

        var semanticModel = context.SemanticModel;

        // Generate cache key based on type content
        var cacheKey = GenerateCacheKey(typeDecl, semanticModel);

        // Try to get from cache first
        if (EntityTransformCache.TryGetCached(cacheKey, out var cachedResult))
        {
            return (cachedResult, cacheKey);
        }

        // Cache miss - perform transformation
        try
        {
            var analyzer = new EntityAnalyzer();
            var entityModel = analyzer.AnalyzeEntity(typeDecl, semanticModel);

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
    /// Generates a cache key based on type declaration and semantic information.
    /// </summary>
    private static string GenerateCacheKey(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
        var typeName = typeDecl.Identifier.ValueText;
        var namespaceName = GetNamespace(typeDecl);

        // Include attribute information in cache key
        var attributeInfo = string.Join("|",
            typeDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Select(a => a.Name.ToString()));

        // Include property information in cache key
        var propertyInfo = string.Join("|",
            typeDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .Select(p => $"{p.Identifier.ValueText}:{p.Type}"));

        // Include type kind (class vs record) in cache key
        var typeKind = typeDecl is RecordDeclarationSyntax ? "record" : "class";

        return $"{namespaceName}.{typeName}#{typeKind}#{attributeInfo}#{propertyInfo}".GetHashCode().ToString();
    }

    /// <summary>
    /// Gets the namespace for a type declaration.
    /// </summary>
    private static string GetNamespace(TypeDeclarationSyntax typeDecl)
    {
        var namespaceDecl = typeDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        var fileScopedNamespace = typeDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
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