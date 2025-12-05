using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Utility class for resolving entity metadata from source-generated code.
/// Uses compile-time interface constraints for AOT compatibility.
/// </summary>
internal static class MetadataResolver
{
    /// <summary>
    /// Retrieves entity metadata from the entity type's generated GetEntityMetadata() method.
    /// This method uses the IEntityMetadataProvider interface constraint for compile-time safety
    /// and AOT compatibility, avoiding reflection.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IEntityMetadataProvider.</typeparam>
    /// <returns>The entity metadata.</returns>
    public static EntityMetadata GetEntityMetadata<TEntity>()
        where TEntity : IEntityMetadataProvider
    {
        return TEntity.GetEntityMetadata();
    }
}
