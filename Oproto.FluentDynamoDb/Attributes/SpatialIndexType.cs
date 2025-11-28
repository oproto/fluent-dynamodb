namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies the type of spatial indexing system to use for encoding geographic coordinates.
/// </summary>
/// <remarks>
/// <para>
/// Different spatial indexing systems have different characteristics and trade-offs:
/// </para>
/// <list type="bullet">
/// <item>
/// <description><strong>GeoHash</strong>: Simple base-32 encoding using Z-order curve. Good general-purpose choice with wide tool support.</description>
/// </item>
/// <item>
/// <description><strong>S2</strong>: Google's spherical geometry system using Hilbert curve. Better area uniformity, especially near poles.</description>
/// </item>
/// <item>
/// <description><strong>H3</strong>: Uber's hexagonal hierarchical spatial index. Provides more uniform neighbor distances and better coverage.</description>
/// </item>
/// </list>
/// </remarks>
public enum SpatialIndexType
{
    /// <summary>
    /// GeoHash encoding using base-32 Z-order curve.
    /// Default for backward compatibility.
    /// </summary>
    GeoHash = 0,

    /// <summary>
    /// Google S2 geometry cells using Hilbert curve.
    /// Provides better area uniformity than GeoHash, especially near poles.
    /// </summary>
    S2 = 1,

    /// <summary>
    /// Uber H3 hexagonal hierarchical spatial index.
    /// Uses hexagons instead of rectangles for more uniform neighbor distances.
    /// </summary>
    H3 = 2
}
