using System.Text.Json.Serialization;
using JsonBlobDemo.Entities;

namespace JsonBlobDemo;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible serialization.
/// 
/// This context enables System.Text.Json to serialize and deserialize the
/// DocumentMetadata type without runtime reflection, making it compatible
/// with Native AOT and trimmed applications.
/// </summary>
/// <remarks>
/// <para>
/// To use this context with FluentDynamoDb:
/// </para>
/// <code>
/// var options = new FluentDynamoDbOptions()
///     .WithSystemTextJson(DocumentJsonContext.Default);
/// </code>
/// </remarks>
[JsonSerializable(typeof(DocumentMetadata))]
[JsonSerializable(typeof(NestedInfo))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class DocumentJsonContext : JsonSerializerContext
{
}
