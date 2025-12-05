using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.SystemTextJson;

/// <summary>
/// System.Text.Json implementation of <see cref="IJsonBlobSerializer"/>.
/// Supports both reflection-based and AOT-compatible serialization.
/// </summary>
/// <remarks>
/// <para>
/// This serializer provides three modes of operation:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Default mode: Uses System.Text.Json with default options</description>
/// </item>
/// <item>
/// <description>Custom options mode: Uses provided <see cref="JsonSerializerOptions"/> for customization</description>
/// </item>
/// <item>
/// <description>AOT mode: Uses a <see cref="JsonSerializerContext"/> for AOT-compatible serialization</description>
/// </item>
/// </list>
/// <para>
/// For AOT scenarios, use the constructor accepting <see cref="JsonSerializerContext"/> to ensure
/// the serializer works correctly in trimmed and Native AOT applications.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default options
/// var serializer = new SystemTextJsonBlobSerializer();
/// 
/// // Custom options
/// var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
/// var serializer = new SystemTextJsonBlobSerializer(options);
/// 
/// // AOT-compatible with JsonSerializerContext
/// var serializer = new SystemTextJsonBlobSerializer(MyJsonContext.Default);
/// </code>
/// </example>
public sealed class SystemTextJsonBlobSerializer : IJsonBlobSerializer
{
    private readonly JsonSerializerOptions? _options;
    private readonly JsonSerializerContext? _context;

    /// <summary>
    /// Creates a serializer with default System.Text.Json options.
    /// </summary>
    /// <remarks>
    /// Uses the default <see cref="JsonSerializerOptions"/> behavior from System.Text.Json.
    /// For AOT scenarios, use the constructor accepting <see cref="JsonSerializerContext"/> instead.
    /// </remarks>
    public SystemTextJsonBlobSerializer()
    {
        _options = null;
        _context = null;
    }

    /// <summary>
    /// Creates a serializer with custom <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for serialization and deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <remarks>
    /// Use this constructor to customize serialization behavior such as:
    /// <list type="bullet">
    /// <item><description>Property naming policy (camelCase, etc.)</description></item>
    /// <item><description>Null value handling</description></item>
    /// <item><description>Custom converters</description></item>
    /// <item><description>Indentation and formatting</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new JsonSerializerOptions
    /// {
    ///     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    ///     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    /// };
    /// var serializer = new SystemTextJsonBlobSerializer(options);
    /// </code>
    /// </example>
    public SystemTextJsonBlobSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _context = null;
    }

    /// <summary>
    /// Creates an AOT-compatible serializer with a <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <param name="context">The JSON serializer context for AOT-compatible serialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Use this constructor for Native AOT and trimmed applications. The <see cref="JsonSerializerContext"/>
    /// provides pre-generated serialization metadata that doesn't require runtime reflection.
    /// </para>
    /// <para>
    /// Create a context by defining a partial class with the <see cref="JsonSerializableAttribute"/>
    /// for each type that needs to be serialized.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [JsonSerializable(typeof(MyComplexType))]
    /// [JsonSerializable(typeof(AnotherType))]
    /// public partial class MyJsonContext : JsonSerializerContext { }
    /// 
    /// var serializer = new SystemTextJsonBlobSerializer(MyJsonContext.Default);
    /// </code>
    /// </example>
    public SystemTextJsonBlobSerializer(JsonSerializerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Serializes the value to a JSON string using the configured options or context.
    /// When using AOT mode with a <see cref="JsonSerializerContext"/>, ensure the type
    /// is registered in the context.
    /// </remarks>
    public string Serialize<T>(T value)
    {
        if (_context != null)
        {
            return JsonSerializer.Serialize(value, typeof(T), _context);
        }

        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deserializes the JSON string to an object of type <typeparamref name="T"/>.
    /// Returns <c>default</c> if the input is null or empty.
    /// When using AOT mode with a <see cref="JsonSerializerContext"/>, ensure the type
    /// is registered in the context.
    /// </remarks>
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        if (_context != null)
        {
            return (T?)JsonSerializer.Deserialize(json, typeof(T), _context);
        }

        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
