using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oproto.FluentDynamoDb.SystemTextJson;

/// <summary>
/// Extension methods for configuring System.Text.Json serialization with <see cref="FluentDynamoDbOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods provide a fluent way to configure JSON serialization for [JsonBlob] properties
/// using System.Text.Json. The serializer is used when converting entities to and from DynamoDB.
/// </para>
/// <para>
/// Three configuration modes are available:
/// </para>
/// <list type="bullet">
/// <item>
/// <description><see cref="WithSystemTextJson(FluentDynamoDbOptions)"/>: Uses default System.Text.Json options</description>
/// </item>
/// <item>
/// <description><see cref="WithSystemTextJson(FluentDynamoDbOptions, JsonSerializerOptions)"/>: Uses custom options for fine-grained control</description>
/// </item>
/// <item>
/// <description><see cref="WithSystemTextJson(FluentDynamoDbOptions, JsonSerializerContext)"/>: Uses a context for AOT-compatible serialization</description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Default options
/// var options = new FluentDynamoDbOptions().WithSystemTextJson();
/// 
/// // Custom options with camelCase
/// var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
/// var options = new FluentDynamoDbOptions().WithSystemTextJson(jsonOptions);
/// 
/// // AOT-compatible with JsonSerializerContext
/// var options = new FluentDynamoDbOptions().WithSystemTextJson(MyJsonContext.Default);
/// </code>
/// </example>
public static class SystemTextJsonOptionsExtensions
{
    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with default options.
    /// </summary>
    /// <param name="options">The FluentDynamoDb options to configure.</param>
    /// <returns>A new <see cref="FluentDynamoDbOptions"/> instance with System.Text.Json configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <remarks>
    /// Uses the default <see cref="JsonSerializerOptions"/> behavior from System.Text.Json.
    /// For AOT scenarios, use the overload accepting <see cref="JsonSerializerContext"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions().WithSystemTextJson();
    /// var table = new MyTable(dynamoDbClient, "TableName", options);
    /// </code>
    /// </example>
    public static FluentDynamoDbOptions WithSystemTextJson(this FluentDynamoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer());
    }

    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with custom options.
    /// </summary>
    /// <param name="options">The FluentDynamoDb options to configure.</param>
    /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> to use for serialization.</param>
    /// <returns>A new <see cref="FluentDynamoDbOptions"/> instance with System.Text.Json configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="serializerOptions"/> is null.</exception>
    /// <remarks>
    /// Use this overload to customize serialization behavior such as:
    /// <list type="bullet">
    /// <item><description>Property naming policy (camelCase, etc.)</description></item>
    /// <item><description>Null value handling</description></item>
    /// <item><description>Custom converters</description></item>
    /// <item><description>Indentation and formatting</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var jsonOptions = new JsonSerializerOptions
    /// {
    ///     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    ///     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    /// };
    /// var options = new FluentDynamoDbOptions().WithSystemTextJson(jsonOptions);
    /// var table = new MyTable(dynamoDbClient, "TableName", options);
    /// </code>
    /// </example>
    public static FluentDynamoDbOptions WithSystemTextJson(
        this FluentDynamoDbOptions options,
        JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer(serializerOptions));
    }

    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with an AOT-compatible context.
    /// </summary>
    /// <param name="options">The FluentDynamoDb options to configure.</param>
    /// <param name="context">The <see cref="JsonSerializerContext"/> for AOT-compatible serialization.</param>
    /// <returns>A new <see cref="FluentDynamoDbOptions"/> instance with System.Text.Json configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Use this overload for Native AOT and trimmed applications. The <see cref="JsonSerializerContext"/>
    /// provides pre-generated serialization metadata that doesn't require runtime reflection.
    /// </para>
    /// <para>
    /// Create a context by defining a partial class with the <see cref="JsonSerializableAttribute"/>
    /// for each type that needs to be serialized.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define a JsonSerializerContext for your types
    /// [JsonSerializable(typeof(MyComplexType))]
    /// [JsonSerializable(typeof(AnotherType))]
    /// public partial class MyJsonContext : JsonSerializerContext { }
    /// 
    /// // Use the context with FluentDynamoDbOptions
    /// var options = new FluentDynamoDbOptions().WithSystemTextJson(MyJsonContext.Default);
    /// var table = new MyTable(dynamoDbClient, "TableName", options);
    /// </code>
    /// </example>
    public static FluentDynamoDbOptions WithSystemTextJson(
        this FluentDynamoDbOptions options,
        JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer(context));
    }
}
