using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates JsonSerializerContext classes for System.Text.Json AOT compatibility.
/// </summary>
internal static class JsonSerializerContextGenerator
{
    /// <summary>
    /// Generates a JsonSerializerContext class for an entity with JSON blob properties.
    /// </summary>
    /// <param name="entity">The entity model.</param>
    /// <returns>The generated C# source code, or null if no JSON blob properties exist.</returns>
    public static string? GenerateJsonSerializerContext(EntityModel entity)
    {
        // Check if entity uses System.Text.Json
        if (entity.JsonSerializerInfo?.SerializerToUse != Analysis.JsonSerializerType.SystemTextJson)
        {
            return null;
        }

        // Get all properties with JsonBlob attribute
        var jsonBlobProperties = entity.Properties
            .Where(p => p.AdvancedType?.IsJsonBlob == true)
            .ToArray();

        if (jsonBlobProperties.Length == 0)
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
