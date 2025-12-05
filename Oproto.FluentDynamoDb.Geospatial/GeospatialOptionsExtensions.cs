namespace Oproto.FluentDynamoDb;

/// <summary>
/// Extension methods for adding geospatial support to <see cref="FluentDynamoDbOptions"/>.
/// </summary>
public static class GeospatialOptionsExtensions
{
    /// <summary>
    /// Adds geospatial support (GeoHash, S2, H3) to FluentDynamoDb.
    /// </summary>
    /// <param name="options">The options to extend.</param>
    /// <returns>A new FluentDynamoDbOptions instance with geospatial support enabled.</returns>
    /// <remarks>
    /// This method registers the <see cref="Geospatial.DefaultGeospatialProvider"/> which provides
    /// implementations for GeoHash, S2, and H3 spatial indexing. Once registered, geospatial
    /// features can be used in expression translation without reflection.
    /// 
    /// Example usage:
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .AddGeospatial()
    ///     .WithLogger(myLogger);
    /// 
    /// var table = new MyTable(dynamoDbClient, "my-table", options);
    /// </code>
    /// </remarks>
    public static FluentDynamoDbOptions AddGeospatial(this FluentDynamoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithGeospatialProvider(new Geospatial.DefaultGeospatialProvider());
    }

    /// <summary>
    /// Adds geospatial support with a custom provider.
    /// </summary>
    /// <param name="options">The options to extend.</param>
    /// <param name="provider">The custom geospatial provider to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified geospatial provider.</returns>
    /// <remarks>
    /// Use this method when you need to provide a custom implementation of <see cref="IGeospatialProvider"/>,
    /// for example for testing or when using a different spatial indexing library.
    /// 
    /// Example usage:
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .AddGeospatial(new MyCustomGeospatialProvider());
    /// </code>
    /// </remarks>
    public static FluentDynamoDbOptions AddGeospatial(
        this FluentDynamoDbOptions options,
        IGeospatialProvider provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);
        
        // Use the internal method from FluentDynamoDbOptions
        // This is accessible because of InternalsVisibleTo
        return options.WithGeospatialProvider(provider);
    }
}
