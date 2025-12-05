namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Interface for entities that provide their own metadata.
/// Implemented by source-generated entity classes.
/// This interface enables compile-time type safety and AOT compatibility
/// by avoiding reflection-based metadata discovery.
/// </summary>
public interface IEntityMetadataProvider
{
    /// <summary>
    /// Gets the entity metadata for this type.
    /// </summary>
    /// <returns>Comprehensive metadata about the entity.</returns>
    static abstract EntityMetadata GetEntityMetadata();
}
