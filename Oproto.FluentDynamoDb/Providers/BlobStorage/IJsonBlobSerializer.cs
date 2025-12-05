namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Interface for JSON serialization of [JsonBlob] properties.
/// Implementations are provided by Oproto.FluentDynamoDb.SystemTextJson 
/// and Oproto.FluentDynamoDb.NewtonsoftJson packages.
/// </summary>
/// <remarks>
/// Configure a JSON serializer using extension methods on <see cref="FluentDynamoDbOptions"/>:
/// <list type="bullet">
/// <item><description>Use <c>.WithSystemTextJson()</c> from the SystemTextJson package</description></item>
/// <item><description>Use <c>.WithNewtonsoftJson()</c> from the NewtonsoftJson package</description></item>
/// </list>
/// </remarks>
public interface IJsonBlobSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>JSON string representation of the object.</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or null if json is null or empty.</returns>
    T? Deserialize<T>(string json);
}
