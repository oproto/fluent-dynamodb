using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates IAsyncEntityHydrator implementations for entities with blob references.
/// This enables AOT-compatible async entity hydration without reflection.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <para>
/// The HydratorGenerator creates implementations of IAsyncEntityHydrator&lt;T&gt; for entities
/// that have blob reference properties. This allows the library to hydrate entities with
/// blob storage support without using reflection to discover the FromDynamoDbAsync method.
/// </para>
/// <para><strong>Generated Code:</strong></para>
/// <list type="bullet">
/// <item><description>Hydrator class implementing IAsyncEntityHydrator&lt;T&gt;</description></item>
/// <item><description>Static registration method for registering the hydrator</description></item>
/// </list>
/// </remarks>
internal static class HydratorGenerator
{
    /// <summary>
    /// Determines if an entity requires a hydrator (has blob reference properties).
    /// </summary>
    /// <param name="entity">The entity model to check.</param>
    /// <returns>True if the entity has blob reference properties, false otherwise.</returns>
    public static bool RequiresHydrator(EntityModel entity)
    {
        return entity.Properties.Any(p => p.ComplexType?.IsBlobReference == true);
    }

    /// <summary>
    /// Generates the IAsyncEntityHydrator implementation for an entity with blob references.
    /// </summary>
    /// <param name="entity">The entity model to generate the hydrator for.</param>
    /// <returns>The generated C# source code, or null if the entity doesn't require a hydrator.</returns>
    public static string? GenerateHydrator(EntityModel entity)
    {
        if (!RequiresHydrator(entity))
        {
            return null;
        }

        var sb = new StringBuilder();

        // File header
        FileHeaderGenerator.GenerateFileHeader(sb);

        // Using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Hydration;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.BlobStorage;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // Hydrator class
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Generated IAsyncEntityHydrator implementation for {entity.ClassName}.");
        sb.AppendLine($"    /// Provides AOT-compatible async entity hydration with blob storage support.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public sealed class {entity.ClassName}Hydrator : IAsyncEntityHydrator<{entity.ClassName}>");
        sb.AppendLine("    {");

        // Singleton instance
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets the singleton instance of the hydrator.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public static readonly {entity.ClassName}Hydrator Instance = new();");
        sb.AppendLine();

        // Private constructor
        sb.AppendLine($"        private {entity.ClassName}Hydrator() {{ }}");
        sb.AppendLine();

        // HydrateAsync for single item
        GenerateHydrateAsyncSingleMethod(sb, entity);

        // HydrateAsync for multiple items
        GenerateHydrateAsyncMultiMethod(sb, entity);

        // SerializeAsync for entity to DynamoDB
        GenerateSerializeAsyncMethod(sb, entity);

        // Close hydrator class
        sb.AppendLine("    }");
        sb.AppendLine();

        // Extension method for registration
        GenerateRegistrationExtension(sb, entity);

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateHydrateAsyncSingleMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Hydrates a {entity.ClassName} from DynamoDB attributes, loading blob references.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"item\">The DynamoDB item attributes.</param>");
        sb.AppendLine($"        /// <param name=\"blobProvider\">The blob storage provider for loading blob references.</param>");
        sb.AppendLine($"        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc.</param>");
        sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"        /// <returns>The hydrated entity.</returns>");
        sb.AppendLine($"        public async Task<{entity.ClassName}> HydrateAsync(");
        sb.AppendLine($"            Dictionary<string, AttributeValue> item,");
        sb.AppendLine($"            IBlobStorageProvider blobProvider,");
        sb.AppendLine($"            FluentDynamoDbOptions? options = null,");
        sb.AppendLine($"            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(item);");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(blobProvider);");
        sb.AppendLine();
        sb.AppendLine($"            // Delegate to the generated FromDynamoDbAsync method on the entity");
        sb.AppendLine($"            return await {entity.ClassName}.FromDynamoDbAsync<{entity.ClassName}>(");
        sb.AppendLine($"                item,");
        sb.AppendLine($"                blobProvider,");
        sb.AppendLine($"                fieldEncryptor: options?.FieldEncryptor,");
        sb.AppendLine($"                options: options,");
        sb.AppendLine($"                cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void GenerateHydrateAsyncMultiMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Hydrates a {entity.ClassName} from multiple DynamoDB items (composite entities).");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"items\">The list of DynamoDB item attributes.</param>");
        sb.AppendLine($"        /// <param name=\"blobProvider\">The blob storage provider for loading blob references.</param>");
        sb.AppendLine($"        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc.</param>");
        sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"        /// <returns>The hydrated entity.</returns>");
        sb.AppendLine($"        public async Task<{entity.ClassName}> HydrateAsync(");
        sb.AppendLine($"            IList<Dictionary<string, AttributeValue>> items,");
        sb.AppendLine($"            IBlobStorageProvider blobProvider,");
        sb.AppendLine($"            FluentDynamoDbOptions? options = null,");
        sb.AppendLine($"            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(items);");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(blobProvider);");
        sb.AppendLine();
        sb.AppendLine($"            if (items.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                throw new ArgumentException(\"Items collection cannot be empty.\", nameof(items));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            // Delegate to the generated FromDynamoDbAsync method on the entity");
        sb.AppendLine($"            return await {entity.ClassName}.FromDynamoDbAsync<{entity.ClassName}>(");
        sb.AppendLine($"                items,");
        sb.AppendLine($"                blobProvider,");
        sb.AppendLine($"                fieldEncryptor: options?.FieldEncryptor,");
        sb.AppendLine($"                options: options,");
        sb.AppendLine($"                cancellationToken);");
        sb.AppendLine("        }");
    }

    private static void GenerateSerializeAsyncMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Serializes a {entity.ClassName} to DynamoDB attributes, storing blob references.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"entity\">The entity to serialize.</param>");
        sb.AppendLine($"        /// <param name=\"blobProvider\">The blob storage provider for storing blob references.</param>");
        sb.AppendLine($"        /// <param name=\"options\">Optional configuration options including logger, JSON serializer, etc.</param>");
        sb.AppendLine($"        /// <param name=\"cancellationToken\">Cancellation token.</param>");
        sb.AppendLine($"        /// <returns>The DynamoDB attributes.</returns>");
        sb.AppendLine($"        public async Task<Dictionary<string, AttributeValue>> SerializeAsync(");
        sb.AppendLine($"            {entity.ClassName} entity,");
        sb.AppendLine($"            IBlobStorageProvider blobProvider,");
        sb.AppendLine($"            FluentDynamoDbOptions? options = null,");
        sb.AppendLine($"            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(entity);");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(blobProvider);");
        sb.AppendLine();
        sb.AppendLine($"            // Delegate to the generated ToDynamoDbAsync method on the entity");
        sb.AppendLine($"            return await {entity.ClassName}.ToDynamoDbAsync(");
        sb.AppendLine($"                entity,");
        sb.AppendLine($"                blobProvider,");
        sb.AppendLine($"                options,");
        sb.AppendLine($"                cancellationToken);");
        sb.AppendLine("        }");
    }

    private static void GenerateRegistrationExtension(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Extension methods for registering {entity.ClassName} hydrator.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static class {entity.ClassName}HydratorExtensions");
        sb.AppendLine("    {");
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Registers the {entity.ClassName} hydrator with the specified registry.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"registry\">The hydrator registry to register with.</param>");
        sb.AppendLine($"        /// <returns>The registry for method chaining.</returns>");
        sb.AppendLine($"        public static IEntityHydratorRegistry Register{entity.ClassName}Hydrator(this IEntityHydratorRegistry registry)");
        sb.AppendLine("        {");
        sb.AppendLine($"            ArgumentNullException.ThrowIfNull(registry);");
        sb.AppendLine($"            registry.Register({entity.ClassName}Hydrator.Instance);");
        sb.AppendLine($"            return registry;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }
}
