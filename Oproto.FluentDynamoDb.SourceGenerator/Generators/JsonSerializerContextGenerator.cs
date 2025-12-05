using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates JsonSerializerContext classes for System.Text.Json AOT compatibility.
/// Note: This generator is kept for backward compatibility but JSON serialization
/// is now configured at runtime via FluentDynamoDbOptions.WithSystemTextJson(JsonSerializerContext).
/// Users should create their own JsonSerializerContext and pass it to WithSystemTextJson().
/// </summary>
internal static class JsonSerializerContextGenerator
{
    /// <summary>
    /// Generates a JsonSerializerContext class for an entity with JSON blob properties.
    /// This is a convenience generator that creates a basic context for entities with [JsonBlob] properties.
    /// For production AOT scenarios, users should create their own JsonSerializerContext with proper configuration.
    /// </summary>
    /// <param name="entity">The entity model.</param>
    /// <returns>The generated C# source code, or null if no JSON blob properties exist.</returns>
    public static string? GenerateJsonSerializerContext(EntityModel entity)
    {
        // Get all properties with JsonBlob attribute
        var jsonBlobProperties = entity.Properties
            .Where(p => p.AdvancedType?.IsJsonBlob == true)
            .ToArray();

        if (jsonBlobProperties.Length == 0)
        {
            return null;
        }

        // Check if System.Text.Json package is referenced
        var hasSystemTextJson = entity.SemanticModel?.Compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.SystemTextJson", StringComparison.OrdinalIgnoreCase)) ?? false;

        if (!hasSystemTextJson)
        {
            return null;
        }

        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);

        // Using statements
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // Generate JsonSerializerContext class
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// JSON serializer context for {entity.ClassName} with AOT support.");
        sb.AppendLine($"    /// This context enables System.Text.Json to work with Native AOT compilation.");
        sb.AppendLine($"    /// For production use, consider creating your own JsonSerializerContext with custom options");
        sb.AppendLine($"    /// and passing it to FluentDynamoDbOptions.WithSystemTextJson(yourContext).");
        sb.AppendLine($"    /// </summary>");

        // Add JsonSerializable attributes for each JSON blob property type
        foreach (var property in jsonBlobProperties)
        {
            var propertyType = GetBaseType(property.PropertyType);
            sb.AppendLine($"    [JsonSerializable(typeof({propertyType}))]");
        }

        // Class declaration - must be partial for source generation
        sb.AppendLine($"    internal partial class {entity.ClassName}JsonContext : JsonSerializerContext");
        sb.AppendLine("    {");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the base type name without nullable annotations.
    /// </summary>
    private static string GetBaseType(string typeName)
    {
        return typeName.TrimEnd('?');
    }
}
