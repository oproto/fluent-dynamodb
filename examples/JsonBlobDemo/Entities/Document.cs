using Oproto.FluentDynamoDb.Attributes;

namespace JsonBlobDemo.Entities;

/// <summary>
/// Represents a document with JSON blob metadata stored in DynamoDB.
/// 
/// This entity demonstrates the [JsonBlob] attribute for storing complex objects
/// as JSON strings in DynamoDB. The metadata property contains nested objects,
/// lists, and dictionaries that are serialized to JSON.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Attribute Usage:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DynamoDbTableAttribute"/> - Specifies the DynamoDB table name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScannableAttribute"/> - Enables Scan() operations for listing all documents.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="JsonBlobAttribute"/> - Marks the Metadata property for JSON serialization.
/// The complex object is serialized to a JSON string before storing in DynamoDB.
/// </description>
/// </item>
/// </list>
/// </remarks>
[DynamoDbTable("json-blob-demo", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "Documents")]
public partial class Document
{
    /// <summary>
    /// Gets or sets the unique identifier for the document.
    /// This serves as the partition key for the DynamoDB table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the document.
    /// </summary>
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the complex metadata object.
    /// This property is serialized as JSON using the configured serializer.
    /// </summary>
    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public DocumentMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the document was created.
    /// </summary>
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was last updated.
    /// </summary>
    [DynamoDbAttribute("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
