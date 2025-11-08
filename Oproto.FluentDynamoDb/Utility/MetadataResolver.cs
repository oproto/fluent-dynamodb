using System.Reflection;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Utility class for resolving entity metadata from source-generated code.
/// </summary>
internal static class MetadataResolver
{
    /// <summary>
    /// Attempts to retrieve entity metadata from the entity type's generated GetEntityMetadata() method.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The entity metadata if available, otherwise null.</returns>
    public static EntityMetadata? GetEntityMetadata<TEntity>()
    {
        try
        {
            var entityType = typeof(TEntity);
            
            // Look for the generated GetEntityMetadata() static method
            var method = entityType.GetMethod(
                "GetEntityMetadata",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            
            if (method == null || method.ReturnType != typeof(EntityMetadata))
            {
                return null;
            }
            
            // Invoke the method to get the metadata
            var metadata = method.Invoke(null, null) as EntityMetadata;
            return metadata;
        }
        catch
        {
            // If anything goes wrong (method not found, invocation error, etc.), return null
            // This allows the code to work with entities that don't have generated metadata
            return null;
        }
    }
}
