namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies the JSON serializer to use for JsonBlob properties.
/// </summary>
public enum JsonSerializerType
{
    /// <summary>
    /// System.Text.Json serializer with full AOT compatibility.
    /// Requires Oproto.FluentDynamoDb.SystemTextJson package.
    /// Recommended for Native AOT deployments.
    /// </summary>
    SystemTextJson,
    
    /// <summary>
    /// Newtonsoft.Json serializer with limited AOT support.
    /// Requires Oproto.FluentDynamoDb.NewtonsoftJson package.
    /// Uses runtime reflection - use for compatibility only.
    /// </summary>
    NewtonsoftJson
}
