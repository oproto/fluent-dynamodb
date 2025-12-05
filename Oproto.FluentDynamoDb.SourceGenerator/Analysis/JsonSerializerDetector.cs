using Microsoft.CodeAnalysis;
using System.Linq;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Detects JSON serializer package references for diagnostic purposes.
/// JSON serialization is now configured at runtime via FluentDynamoDbOptions.WithJsonSerializer().
/// This detector is used only to emit warnings when [JsonBlob] is used without a JSON package reference.
/// </summary>
internal static class JsonSerializerDetector
{
    /// <summary>
    /// Detects which JSON serializer packages are referenced in the compilation.
    /// Used for diagnostic purposes only - code generation no longer depends on this.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <returns>Information about the detected JSON serializer packages.</returns>
    public static JsonSerializerInfo DetectJsonSerializer(Compilation compilation)
    {
        var info = new JsonSerializerInfo();

        // Check for package references
        info.HasSystemTextJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.SystemTextJson", StringComparison.OrdinalIgnoreCase));

        info.HasNewtonsoftJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.NewtonsoftJson", StringComparison.OrdinalIgnoreCase));

        return info;
    }
}

/// <summary>
/// Information about detected JSON serializer package references.
/// Used for diagnostic purposes only.
/// </summary>
internal class JsonSerializerInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether Oproto.FluentDynamoDb.SystemTextJson is referenced.
    /// </summary>
    public bool HasSystemTextJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Oproto.FluentDynamoDb.NewtonsoftJson is referenced.
    /// </summary>
    public bool HasNewtonsoftJson { get; set; }

    /// <summary>
    /// Gets a value indicating whether any JSON serializer package is referenced.
    /// </summary>
    public bool HasAnySerializer => HasSystemTextJson || HasNewtonsoftJson;
}
