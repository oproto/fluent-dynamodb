namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities.Builders;

/// <summary>
/// Fluent builder for creating HashSetTestEntity instances with sensible defaults.
/// </summary>
public class HashSetTestEntityBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private HashSet<int>? _categoryIds;
    private HashSet<string>? _tags;
    private HashSet<byte[]>? _binaryData;

    /// <summary>
    /// Sets the partition key (Id) for the entity.
    /// </summary>
    public HashSetTestEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the CategoryIds property with the specified integer values.
    /// </summary>
    public HashSetTestEntityBuilder WithCategoryIds(params int[] ids)
    {
        _categoryIds = new HashSet<int>(ids);
        return this;
    }

    /// <summary>
    /// Sets the Tags property with the specified string values.
    /// </summary>
    public HashSetTestEntityBuilder WithTags(params string[] tags)
    {
        _tags = new HashSet<string>(tags);
        return this;
    }

    /// <summary>
    /// Sets the BinaryData property with the specified byte arrays.
    /// </summary>
    public HashSetTestEntityBuilder WithBinaryData(params byte[][] data)
    {
        _binaryData = new HashSet<byte[]>(data);
        return this;
    }

    /// <summary>
    /// Sets CategoryIds to null explicitly.
    /// </summary>
    public HashSetTestEntityBuilder WithNullCategoryIds()
    {
        _categoryIds = null;
        return this;
    }

    /// <summary>
    /// Sets Tags to null explicitly.
    /// </summary>
    public HashSetTestEntityBuilder WithNullTags()
    {
        _tags = null;
        return this;
    }

    /// <summary>
    /// Sets BinaryData to null explicitly.
    /// </summary>
    public HashSetTestEntityBuilder WithNullBinaryData()
    {
        _binaryData = null;
        return this;
    }

    /// <summary>
    /// Sets CategoryIds to an empty HashSet.
    /// </summary>
    public HashSetTestEntityBuilder WithEmptyCategoryIds()
    {
        _categoryIds = new HashSet<int>();
        return this;
    }

    /// <summary>
    /// Sets Tags to an empty HashSet.
    /// </summary>
    public HashSetTestEntityBuilder WithEmptyTags()
    {
        _tags = new HashSet<string>();
        return this;
    }

    /// <summary>
    /// Sets BinaryData to an empty HashSet.
    /// </summary>
    public HashSetTestEntityBuilder WithEmptyBinaryData()
    {
        _binaryData = new HashSet<byte[]>();
        return this;
    }

    /// <summary>
    /// Builds the HashSetTestEntity with the configured values.
    /// Default: Only Id is set, all collections are null.
    /// </summary>
    public HashSetTestEntity Build()
    {
        return new HashSetTestEntity
        {
            Id = _id,
            CategoryIds = _categoryIds,
            Tags = _tags,
            BinaryData = _binaryData
        };
    }
}
