using Newtonsoft.Json;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.NewtonsoftJson;

/// <summary>
/// Newtonsoft.Json implementation of <see cref="IJsonBlobSerializer"/>.
/// Provides JSON serialization for [JsonBlob] properties using Newtonsoft.Json.
/// </summary>
/// <remarks>
/// <para>
/// This serializer provides two modes of operation:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Default mode: Uses optimized default settings for DynamoDB storage</description>
/// </item>
/// <item>
/// <description>Custom settings mode: Uses provided <see cref="JsonSerializerSettings"/> for customization</description>
/// </item>
/// </list>
/// <para>
/// The default settings are optimized for DynamoDB storage:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="TypeNameHandling.None"/> - No type information in JSON (security best practice)</description></item>
/// <item><description><see cref="NullValueHandling.Ignore"/> - Omit null values to reduce storage</description></item>
/// <item><description><see cref="DateFormatHandling.IsoDateFormat"/> - ISO 8601 date format for consistency</description></item>
/// <item><description><see cref="ReferenceLoopHandling.Ignore"/> - Prevent circular reference errors</description></item>
/// </list>
/// <para>
/// <strong>Note:</strong> This package uses runtime reflection and has limited AOT compatibility.
/// For full AOT support, use <c>Oproto.FluentDynamoDb.SystemTextJson</c> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default settings (recommended)
/// var serializer = new NewtonsoftJsonBlobSerializer();
/// 
/// // Custom settings
/// var settings = new JsonSerializerSettings
/// {
///     Formatting = Formatting.Indented,
///     NullValueHandling = NullValueHandling.Include
/// };
/// var serializer = new NewtonsoftJsonBlobSerializer(settings);
/// </code>
/// </example>
public sealed class NewtonsoftJsonBlobSerializer : IJsonBlobSerializer
{
    private readonly JsonSerializerSettings _settings;

    /// <summary>
    /// Default settings optimized for DynamoDB storage.
    /// </summary>
    /// <remarks>
    /// These settings are designed for safe, efficient storage in DynamoDB:
    /// <list type="bullet">
    /// <item><description><see cref="TypeNameHandling.None"/> - Prevents type injection vulnerabilities</description></item>
    /// <item><description><see cref="NullValueHandling.Ignore"/> - Reduces storage size by omitting nulls</description></item>
    /// <item><description><see cref="DateFormatHandling.IsoDateFormat"/> - Ensures consistent date formatting</description></item>
    /// <item><description><see cref="ReferenceLoopHandling.Ignore"/> - Handles circular references gracefully</description></item>
    /// </list>
    /// </remarks>
    private static readonly JsonSerializerSettings DefaultSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    /// <summary>
    /// Creates a serializer with default settings optimized for DynamoDB storage.
    /// </summary>
    /// <remarks>
    /// The default settings include:
    /// <list type="bullet">
    /// <item><description><see cref="TypeNameHandling.None"/> - No type metadata (security best practice)</description></item>
    /// <item><description><see cref="NullValueHandling.Ignore"/> - Omit null values</description></item>
    /// <item><description><see cref="DateFormatHandling.IsoDateFormat"/> - ISO 8601 dates</description></item>
    /// <item><description><see cref="ReferenceLoopHandling.Ignore"/> - Handle circular references</description></item>
    /// </list>
    /// </remarks>
    public NewtonsoftJsonBlobSerializer()
    {
        _settings = DefaultSettings;
    }

    /// <summary>
    /// Creates a serializer with custom <see cref="JsonSerializerSettings"/>.
    /// </summary>
    /// <param name="settings">The JSON serializer settings to use for serialization and deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <remarks>
    /// Use this constructor to customize serialization behavior such as:
    /// <list type="bullet">
    /// <item><description>Contract resolvers (camelCase naming, etc.)</description></item>
    /// <item><description>Null value handling</description></item>
    /// <item><description>Custom converters</description></item>
    /// <item><description>Date formatting</description></item>
    /// <item><description>Reference handling</description></item>
    /// </list>
    /// <para>
    /// <strong>Security Warning:</strong> Avoid using <see cref="TypeNameHandling.Auto"/> or 
    /// <see cref="TypeNameHandling.All"/> as they can introduce security vulnerabilities.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var settings = new JsonSerializerSettings
    /// {
    ///     ContractResolver = new CamelCasePropertyNamesContractResolver(),
    ///     NullValueHandling = NullValueHandling.Include,
    ///     Formatting = Formatting.None
    /// };
    /// var serializer = new NewtonsoftJsonBlobSerializer(settings);
    /// </code>
    /// </example>
    public NewtonsoftJsonBlobSerializer(JsonSerializerSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Serializes the value to a JSON string using the configured settings.
    /// </remarks>
    public string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, _settings);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deserializes the JSON string to an object of type <typeparamref name="T"/>.
    /// Returns <c>default</c> if the input is null or empty.
    /// </remarks>
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        return JsonConvert.DeserializeObject<T>(json, _settings);
    }
}
