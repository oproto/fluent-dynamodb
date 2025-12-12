namespace JsonBlobDemo.Entities;

/// <summary>
/// Complex metadata object that will be serialized as JSON in DynamoDB.
/// Demonstrates nested objects, collections, and dictionaries.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Gets or sets the author of the document.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags associated with the document.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets custom key-value fields for extensibility.
    /// </summary>
    public Dictionary<string, string> CustomFields { get; set; } = new();

    /// <summary>
    /// Gets or sets additional nested information.
    /// </summary>
    public NestedInfo? AdditionalInfo { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not DocumentMetadata other)
            return false;

        return Author == other.Author &&
               Tags.SequenceEqual(other.Tags) &&
               CustomFields.Count == other.CustomFields.Count &&
               CustomFields.All(kvp => other.CustomFields.TryGetValue(kvp.Key, out var value) && value == kvp.Value) &&
               Equals(AdditionalInfo, other.AdditionalInfo);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Author, Tags.Count, CustomFields.Count, AdditionalInfo);
    }
}

/// <summary>
/// Nested information demonstrating deep serialization.
/// </summary>
public class NestedInfo
{
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority level.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not NestedInfo other)
            return false;

        return Category == other.Category && Priority == other.Priority;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Category, Priority);
    }
}
