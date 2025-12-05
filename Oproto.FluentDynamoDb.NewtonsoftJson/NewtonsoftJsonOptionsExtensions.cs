using Newtonsoft.Json;

namespace Oproto.FluentDynamoDb.NewtonsoftJson;

/// <summary>
/// Extension methods for configuring Newtonsoft.Json serialization with <see cref="FluentDynamoDbOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods provide a fluent way to configure JSON serialization for [JsonBlob] properties
/// using Newtonsoft.Json. The serializer is used when converting entities to and from DynamoDB.
/// </para>
/// <para>
/// Two configuration modes are available:
/// </para>
/// <list type="bullet">
/// <item>
/// <description><see cref="WithNewtonsoftJson(FluentDynamoDbOptions)"/>: Uses default settings optimized for DynamoDB</description>
/// </item>
/// <item>
/// <description><see cref="WithNewtonsoftJson(FluentDynamoDbOptions, JsonSerializerSettings)"/>: Uses custom settings for fine-grained control</description>
/// </item>
/// </list>
/// <para>
/// <strong>Note:</strong> Newtonsoft.Json uses runtime reflection and has limited AOT compatibility.
/// For full AOT support, use <c>Oproto.FluentDynamoDb.SystemTextJson</c> with a <c>JsonSerializerContext</c> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default settings (recommended)
/// var options = new FluentDynamoDbOptions().WithNewtonsoftJson();
/// 
/// // Custom settings with camelCase
/// var settings = new JsonSerializerSettings
/// {
///     ContractResolver = new CamelCasePropertyNamesContractResolver()
/// };
/// var options = new FluentDynamoDbOptions().WithNewtonsoftJson(settings);
/// </code>
/// </example>
public static class NewtonsoftJsonOptionsExtensions
{
    /// <summary>
    /// Configures Newtonsoft.Json for [JsonBlob] properties with default settings.
    /// </summary>
    /// <param name="options">The FluentDynamoDb options to configure.</param>
    /// <returns>A new <see cref="FluentDynamoDbOptions"/> instance with Newtonsoft.Json configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Uses default settings optimized for DynamoDB storage:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="TypeNameHandling.None"/> - No type metadata (security best practice)</description></item>
    /// <item><description><see cref="NullValueHandling.Ignore"/> - Omit null values to reduce storage</description></item>
    /// <item><description><see cref="DateFormatHandling.IsoDateFormat"/> - ISO 8601 dates for consistency</description></item>
    /// <item><description><see cref="ReferenceLoopHandling.Ignore"/> - Handle circular references gracefully</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions().WithNewtonsoftJson();
    /// var table = new MyTable(dynamoDbClient, "TableName", options);
    /// </code>
    /// </example>
    public static FluentDynamoDbOptions WithNewtonsoftJson(this FluentDynamoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithJsonSerializer(new NewtonsoftJsonBlobSerializer());
    }

    /// <summary>
    /// Configures Newtonsoft.Json for [JsonBlob] properties with custom settings.
    /// </summary>
    /// <param name="options">The FluentDynamoDb options to configure.</param>
    /// <param name="settings">The <see cref="JsonSerializerSettings"/> to use for serialization.</param>
    /// <returns>A new <see cref="FluentDynamoDbOptions"/> instance with Newtonsoft.Json configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="settings"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Use this overload to customize serialization behavior such as:
    /// </para>
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
    /// var options = new FluentDynamoDbOptions().WithNewtonsoftJson(settings);
    /// var table = new MyTable(dynamoDbClient, "TableName", options);
    /// </code>
    /// </example>
    public static FluentDynamoDbOptions WithNewtonsoftJson(
        this FluentDynamoDbOptions options,
        JsonSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        return options.WithJsonSerializer(new NewtonsoftJsonBlobSerializer(settings));
    }
}
