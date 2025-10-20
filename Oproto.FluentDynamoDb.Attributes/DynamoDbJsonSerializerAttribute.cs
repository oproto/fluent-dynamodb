namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Assembly-level attribute to specify the default JSON serializer for all JsonBlob properties.
/// This attribute should be placed at the assembly level in your project.
/// If both SystemTextJson and NewtonsoftJson packages are referenced, this attribute
/// determines which serializer to use.
/// </summary>
/// <example>
/// [assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public class DynamoDbJsonSerializerAttribute : Attribute
{
    /// <summary>
    /// Gets the JSON serializer type to use.
    /// </summary>
    public JsonSerializerType SerializerType { get; }
    
    /// <summary>
    /// Initializes a new instance of the DynamoDbJsonSerializerAttribute class.
    /// </summary>
    /// <param name="serializerType">The JSON serializer type to use.</param>
    public DynamoDbJsonSerializerAttribute(JsonSerializerType serializerType)
    {
        SerializerType = serializerType;
    }
}
