using Microsoft.CodeAnalysis;
using System.Linq;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Detects JSON serializer package references and assembly-level configuration.
/// </summary>
internal class JsonSerializerDetector
{
    /// <summary>
    /// Detects which JSON serializer is configured for the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <returns>Information about the detected JSON serializer configuration.</returns>
    public static JsonSerializerInfo DetectJsonSerializer(Compilation compilation)
    {
        var info = new JsonSerializerInfo();

        // Check for package references
        info.HasSystemTextJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.SystemTextJson", StringComparison.OrdinalIgnoreCase));

        info.HasNewtonsoftJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.NewtonsoftJson", StringComparison.OrdinalIgnoreCase));

        // Check for assembly-level DynamoDbJsonSerializer attribute
        var assemblyAttributes = compilation.Assembly.GetAttributes();
        var jsonSerializerAttribute = assemblyAttributes.FirstOrDefault(attr =>
            attr.AttributeClass?.Name == "DynamoDbJsonSerializerAttribute" ||
            attr.AttributeClass?.ToDisplayString() == "Oproto.FluentDynamoDb.Attributes.DynamoDbJsonSerializerAttribute");

        if (jsonSerializerAttribute != null && jsonSerializerAttribute.ConstructorArguments.Length > 0)
        {
            // Extract the serializer type from the attribute argument
            var serializerTypeValue = jsonSerializerAttribute.ConstructorArguments[0].Value;
            if (serializerTypeValue is int intValue)
            {
                info.AssemblyLevelSerializer = intValue switch
                {
                    0 => JsonSerializerType.SystemTextJson,
                    1 => JsonSerializerType.NewtonsoftJson,
                    _ => JsonSerializerType.None
                };
            }
        }

        // Determine which serializer to use
        info.SerializerToUse = DetermineSerializerToUse(info);

        return info;
    }

    private static JsonSerializerType DetermineSerializerToUse(JsonSerializerInfo info)
    {
        // If assembly-level attribute is specified, use that
        if (info.AssemblyLevelSerializer != JsonSerializerType.None)
        {
            return info.AssemblyLevelSerializer;
        }

        // If only one package is referenced, use that
        if (info.HasSystemTextJson && !info.HasNewtonsoftJson)
        {
            return JsonSerializerType.SystemTextJson;
        }

        if (info.HasNewtonsoftJson && !info.HasSystemTextJson)
        {
            return JsonSerializerType.NewtonsoftJson;
        }

        // If both are referenced, prefer System.Text.Json (better AOT support)
        if (info.HasSystemTextJson && info.HasNewtonsoftJson)
        {
            return JsonSerializerType.SystemTextJson;
        }

        // No serializer available
        return JsonSerializerType.None;
    }
}

/// <summary>
/// Information about detected JSON serializer configuration.
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
    /// Gets or sets the serializer type specified at assembly level, if any.
    /// </summary>
    public JsonSerializerType AssemblyLevelSerializer { get; set; } = JsonSerializerType.None;

    /// <summary>
    /// Gets or sets the serializer type that should be used for code generation.
    /// </summary>
    public JsonSerializerType SerializerToUse { get; set; } = JsonSerializerType.None;

    /// <summary>
    /// Gets a value indicating whether any JSON serializer is available.
    /// </summary>
    public bool HasAnySerializer => HasSystemTextJson || HasNewtonsoftJson;
}

/// <summary>
/// Enum representing the available JSON serializer types.
/// </summary>
internal enum JsonSerializerType
{
    /// <summary>
    /// No serializer configured.
    /// </summary>
    None = -1,

    /// <summary>
    /// System.Text.Json serializer (recommended for AOT).
    /// </summary>
    SystemTextJson = 0,

    /// <summary>
    /// Newtonsoft.Json serializer (limited AOT support).
    /// </summary>
    NewtonsoftJson = 1
}
