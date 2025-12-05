namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Contains information about complex type features for a property.
/// </summary>
internal class ComplexTypeInfo
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this property is a Map type.
    /// </summary>
    public bool IsMap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is a Set type.
    /// </summary>
    public bool IsSet { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is a List type.
    /// </summary>
    public bool IsList { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is marked with [TimeToLive].
    /// </summary>
    public bool IsTtl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is marked with [JsonBlob].
    /// </summary>
    public bool IsJsonBlob { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this property is marked with [BlobReference].
    /// </summary>
    public bool IsBlobReference { get; set; }

    /// <summary>
    /// Gets or sets the element type for collection types (Set or List).
    /// </summary>
    public string? ElementType { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer type (SystemTextJson or NewtonsoftJson).
    /// </summary>
    public string? JsonSerializerType { get; set; }

    /// <summary>
    /// Gets or sets the blob provider configuration.
    /// </summary>
    public BlobProviderConfig? BlobProvider { get; set; }

    /// <summary>
    /// Gets a value indicating whether this property uses any complex type features.
    /// </summary>
    public bool HasComplexType => IsMap || IsSet || IsList || IsTtl || IsJsonBlob || IsBlobReference;
}
