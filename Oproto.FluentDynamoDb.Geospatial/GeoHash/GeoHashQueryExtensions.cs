namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Extension methods for GeoHash spatial queries in lambda expressions.
/// These methods are markers for expression translation and should not be called directly.
/// They will be recognized by the ExpressionTranslator and converted to DynamoDB BETWEEN syntax.
/// </summary>
/// <remarks>
/// <para><strong>How It Works:</strong></para>
/// <para>
/// When you use these methods in a lambda expression, the ExpressionTranslator recognizes them
/// and translates them to the corresponding DynamoDB BETWEEN expression. The methods themselves
/// are never actually executed - they serve as markers in the expression tree.
/// </para>
/// <para><strong>Example Usage:</strong></para>
/// <code>
/// // Proximity query with WithinDistanceKilometers
/// var results = await table.Query&lt;Store&gt;()
///     .Where&lt;Store&gt;(x => x.Region == "west" &amp;&amp; x.Location.WithinDistanceKilometers(center, 5.0))
///     .ToListAsync();
/// 
/// // Bounding box query with WithinBoundingBox
/// var results = await table.Query&lt;Store&gt;()
///     .Where&lt;Store&gt;(x => x.Region == "west" &amp;&amp; x.Location.WithinBoundingBox(southwest, northeast))
///     .ToListAsync();
/// </code>
/// <para>
/// These methods generate efficient single BETWEEN queries because GeoHash forms a continuous
/// lexicographic space-filling curve. This is more efficient than S2/H3 which require multiple
/// discrete cell queries.
/// </para>
/// </remarks>
public static class GeoHashQueryExtensions
{
    /// <summary>
    /// Marker method for proximity queries using GeoHash.
    /// Translates to a BETWEEN query on the GeoHash-indexed attribute.
    /// </summary>
    /// <param name="location">The GeoLocation property to query.</param>
    /// <param name="center">The center point of the search radius.</param>
    /// <param name="radiusKilometers">The search radius in kilometers.</param>
    /// <returns>
    /// This method is never actually executed. It serves as a marker for the ExpressionTranslator.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method should only be used in lambda expressions passed to Query().Where().
    /// It will be translated to a DynamoDB BETWEEN expression that queries the GeoHash range
    /// covering the specified radius.
    /// </para>
    /// <para>
    /// Note: GeoHash BETWEEN queries return a rectangular bounding box approximation.
    /// Results may include locations slightly outside the exact circular radius and should
    /// be post-filtered for exact distance if needed.
    /// </para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var center = new GeoLocation(37.7749, -122.4194);
    /// var results = await table.Query&lt;Store&gt;()
    ///     .Where&lt;Store&gt;(x => x.Region == "west" &amp;&amp; x.Location.WithinDistanceKilometers(center, 5.0))
    ///     .ToListAsync();
    /// 
    /// // Post-filter for exact circular distance
    /// var exactResults = results
    ///     .Where(s => s.Location.DistanceToKilometers(center) &lt;= 5.0)
    ///     .ToList();
    /// </code>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called directly instead of being used in a lambda expression.
    /// </exception>
    public static bool WithinDistanceKilometers(this GeoLocation location, GeoLocation center, double radiusKilometers)
    {
        throw new InvalidOperationException(
            "WithinDistanceKilometers is a marker method for expression translation and should not be called directly. " +
            "Use it only in lambda expressions passed to Query().Where().");
    }

    /// <summary>
    /// Marker method for proximity queries using GeoHash with distance in meters.
    /// Translates to a BETWEEN query on the GeoHash-indexed attribute.
    /// </summary>
    /// <param name="location">The GeoLocation property to query.</param>
    /// <param name="center">The center point of the search radius.</param>
    /// <param name="radiusMeters">The search radius in meters.</param>
    /// <returns>
    /// This method is never actually executed. It serves as a marker for the ExpressionTranslator.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method should only be used in lambda expressions passed to Query().Where().
    /// It will be translated to a DynamoDB BETWEEN expression that queries the GeoHash range
    /// covering the specified radius.
    /// </para>
    /// <para>
    /// Note: GeoHash BETWEEN queries return a rectangular bounding box approximation.
    /// Results may include locations slightly outside the exact circular radius and should
    /// be post-filtered for exact distance if needed.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called directly instead of being used in a lambda expression.
    /// </exception>
    public static bool WithinDistanceMeters(this GeoLocation location, GeoLocation center, double radiusMeters)
    {
        throw new InvalidOperationException(
            "WithinDistanceMeters is a marker method for expression translation and should not be called directly. " +
            "Use it only in lambda expressions passed to Query().Where().");
    }

    /// <summary>
    /// Marker method for proximity queries using GeoHash with distance in miles.
    /// Translates to a BETWEEN query on the GeoHash-indexed attribute.
    /// </summary>
    /// <param name="location">The GeoLocation property to query.</param>
    /// <param name="center">The center point of the search radius.</param>
    /// <param name="radiusMiles">The search radius in miles.</param>
    /// <returns>
    /// This method is never actually executed. It serves as a marker for the ExpressionTranslator.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method should only be used in lambda expressions passed to Query().Where().
    /// It will be translated to a DynamoDB BETWEEN expression that queries the GeoHash range
    /// covering the specified radius.
    /// </para>
    /// <para>
    /// Note: GeoHash BETWEEN queries return a rectangular bounding box approximation.
    /// Results may include locations slightly outside the exact circular radius and should
    /// be post-filtered for exact distance if needed.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called directly instead of being used in a lambda expression.
    /// </exception>
    public static bool WithinDistanceMiles(this GeoLocation location, GeoLocation center, double radiusMiles)
    {
        throw new InvalidOperationException(
            "WithinDistanceMiles is a marker method for expression translation and should not be called directly. " +
            "Use it only in lambda expressions passed to Query().Where().");
    }

    /// <summary>
    /// Marker method for bounding box queries using GeoHash.
    /// Translates to a BETWEEN query on the GeoHash-indexed attribute.
    /// </summary>
    /// <param name="location">The GeoLocation property to query.</param>
    /// <param name="southwest">The southwest corner of the bounding box.</param>
    /// <param name="northeast">The northeast corner of the bounding box.</param>
    /// <returns>
    /// This method is never actually executed. It serves as a marker for the ExpressionTranslator.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method should only be used in lambda expressions passed to Query().Where().
    /// It will be translated to a DynamoDB BETWEEN expression that queries the GeoHash range
    /// covering the specified bounding box.
    /// </para>
    /// <para>
    /// This is particularly efficient for rectangular area queries because GeoHash BETWEEN
    /// queries execute as a single DynamoDB query operation, unlike S2/H3 which require
    /// multiple discrete cell queries.
    /// </para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var southwest = new GeoLocation(37.765, -122.425);
    /// var northeast = new GeoLocation(37.81, -122.40);
    /// var results = await table.Query&lt;Store&gt;()
    ///     .Where&lt;Store&gt;(x => x.Region == "west" &amp;&amp; x.Location.WithinBoundingBox(southwest, northeast))
    ///     .ToListAsync();
    /// </code>
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if this method is called directly instead of being used in a lambda expression.
    /// </exception>
    public static bool WithinBoundingBox(this GeoLocation location, GeoLocation southwest, GeoLocation northeast)
    {
        throw new InvalidOperationException(
            "WithinBoundingBox is a marker method for expression translation and should not be called directly. " +
            "Use it only in lambda expressions passed to Query().Where().");
    }
}
