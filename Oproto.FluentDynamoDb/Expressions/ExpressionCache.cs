using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Cache for translated expressions to avoid repeated analysis.
/// Thread-safe implementation using ConcurrentDictionary with bounded size.
/// </summary>
/// <remarks>
/// <para><strong>Caching Strategy:</strong></para>
/// <para>
/// The cache stores expression structure templates, not parameter values. This means that
/// expressions with the same structure but different values benefit from caching:
/// </para>
/// <code>
/// // First call - translates and caches
/// translator.TranslateWithCache(x => x.Id == userId1, context);
/// 
/// // Second call - uses cached structure, only values differ
/// translator.TranslateWithCache(x => x.Id == userId2, context);
/// </code>
/// 
/// <para><strong>Cache Key:</strong></para>
/// <para>
/// The cache key combines the expression structure (using ToString()) and the validation mode.
/// This ensures that the same expression used in different contexts (Query vs Filter) is
/// cached separately.
/// </para>
/// 
/// <para><strong>Thread Safety:</strong></para>
/// <para>
/// The cache is thread-safe and can be safely accessed from multiple threads concurrently.
/// It uses <see cref="ConcurrentDictionary{TKey,TValue}"/> internally for lock-free reads
/// and writes.
/// </para>
/// 
/// <para><strong>Performance Benefits:</strong></para>
/// <list type="bullet">
/// <item><description>Avoids repeated expression tree traversal</description></item>
/// <item><description>Reduces allocations for expression string building</description></item>
/// <item><description>Improves performance for frequently-used query patterns</description></item>
/// <item><description>Particularly beneficial in high-throughput scenarios</description></item>
/// </list>
/// 
/// <para><strong>Memory Management:</strong></para>
/// <para>
/// The cache is bounded to a configurable maximum size (default: 1024 entries). When the
/// limit is reached, the cache is cleared entirely to reclaim memory. This simple eviction
/// strategy avoids the overhead of LRU tracking while preventing unbounded growth in
/// long-running applications. Most applications use a small number of distinct query patterns,
/// so the cache rarely reaches its limit in practice.
/// </para>
/// <para>
/// To customize the limit, pass a <c>maxSize</c> parameter to the constructor. Each cached
/// entry stores only the expression string template (typically &lt; 1KB).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Access the global cache
/// var cache = ExpressionTranslator.Cache;
/// 
/// // Check cache size
/// Console.WriteLine($"Cached expressions: {cache.Count}");
/// 
/// // Clear cache if needed (e.g., after configuration changes)
/// cache.Clear();
/// 
/// // Use caching in translation
/// var translator = new ExpressionTranslator();
/// var result = translator.TranslateWithCache(x => x.Id == userId, context);
/// // Subsequent calls with same expression structure use cache
/// </code>
/// </example>
public class ExpressionCache
{
    /// <summary>
    /// Default maximum number of cached expressions before eviction.
    /// </summary>
    public const int DefaultMaxSize = 1024;

    private readonly ConcurrentDictionary<ExpressionCacheKey, string> _cache = new();
    private readonly int _maxSize;

    /// <summary>
    /// Creates a new expression cache with the default maximum size.
    /// </summary>
    public ExpressionCache() : this(DefaultMaxSize)
    {
    }

    /// <summary>
    /// Creates a new expression cache with the specified maximum size.
    /// </summary>
    /// <param name="maxSize">
    /// Maximum number of entries before the cache is cleared. Must be greater than zero.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxSize"/> is less than 1.</exception>
    public ExpressionCache(int maxSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize, 1);
        _maxSize = maxSize;
    }

    /// <summary>
    /// Gets a cached expression translation or adds a new one using the provided translator function.
    /// If the cache has reached its maximum size, it is cleared before adding the new entry.
    /// </summary>
    /// <param name="expression">The expression to translate.</param>
    /// <param name="mode">The validation mode for the expression.</param>
    /// <param name="translator">Function to translate the expression if not cached.</param>
    /// <returns>The translated expression string.</returns>
    public string GetOrAdd(
        Expression expression,
        ExpressionValidationMode mode,
        Func<string> translator)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(translator);

        var key = new ExpressionCacheKey(expression, mode);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Evict all entries when the cache is full. This is a simple strategy that avoids
        // the complexity and overhead of LRU tracking. In practice, most applications use
        // a small set of query patterns that will quickly repopulate the cache.
        if (_cache.Count >= _maxSize)
            _cache.Clear();

        return _cache.GetOrAdd(key, _ => translator());
    }

    /// <summary>
    /// Clears all cached expressions.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached expressions.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the maximum number of entries this cache will hold before eviction.
    /// </summary>
    public int MaxSize => _maxSize;
}

/// <summary>
/// Cache key for expression translations.
/// Combines the expression and validation mode to create a unique key.
/// </summary>
internal sealed record ExpressionCacheKey
{
    private readonly Expression _expression;
    private readonly ExpressionValidationMode _mode;
    private readonly int _hashCode;

    public ExpressionCacheKey(Expression expression, ExpressionValidationMode mode)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _mode = mode;
        
        // Pre-compute hash code for performance
        // Use expression's ToString() for structural comparison
        _hashCode = HashCode.Combine(_expression.ToString(), _mode);
    }

    public bool Equals(ExpressionCacheKey? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        // Compare validation mode first (fast)
        if (_mode != other._mode)
            return false;

        // Compare expression structure using ToString()
        // This provides structural equality without deep tree comparison
        return _expression.ToString() == other._expression.ToString();
    }

    public override int GetHashCode() => _hashCode;
}
