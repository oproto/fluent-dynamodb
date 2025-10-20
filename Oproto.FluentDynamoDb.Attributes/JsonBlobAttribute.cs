namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property to be serialized as JSON before storing in DynamoDB.
/// Requires referencing either Oproto.FluentDynamoDb.SystemTextJson or 
/// Oproto.FluentDynamoDb.NewtonsoftJson package.
/// Can be combined with BlobReferenceAttribute to store large JSON objects externally.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class JsonBlobAttribute : Attribute
{
    /// <summary>
    /// Optional maximum size in bytes before using external blob storage.
    /// If the serialized JSON exceeds this threshold and BlobReferenceAttribute is present,
    /// the data will be stored externally instead of inline in DynamoDB.
    /// </summary>
    public int? InlineThreshold { get; set; }
}
