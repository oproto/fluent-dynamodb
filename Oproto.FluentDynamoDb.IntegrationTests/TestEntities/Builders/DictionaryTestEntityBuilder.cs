namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities.Builders;

/// <summary>
/// Fluent builder for creating DictionaryTestEntity instances with sensible defaults.
/// </summary>
public class DictionaryTestEntityBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private Dictionary<string, string>? _metadata;
    private Dictionary<string, string>? _settings;

    /// <summary>
    /// Sets the partition key (Id) for the entity.
    /// </summary>
    public DictionaryTestEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the Metadata property with the specified dictionary.
    /// </summary>
    public DictionaryTestEntityBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        _metadata = new Dictionary<string, string>(metadata);
        return this;
    }

    /// <summary>
    /// Adds a single key-value pair to the Metadata property.
    /// </summary>
    public DictionaryTestEntityBuilder AddMetadata(string key, string value)
    {
        _metadata ??= new Dictionary<string, string>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the Settings property with the specified dictionary.
    /// </summary>
    public DictionaryTestEntityBuilder WithSettings(Dictionary<string, string> settings)
    {
        _settings = new Dictionary<string, string>(settings);
        return this;
    }

    /// <summary>
    /// Adds a single key-value pair to the Settings property.
    /// </summary>
    public DictionaryTestEntityBuilder AddSetting(string key, string value)
    {
        _settings ??= new Dictionary<string, string>();
        _settings[key] = value;
        return this;
    }

    /// <summary>
    /// Sets Metadata to null explicitly.
    /// </summary>
    public DictionaryTestEntityBuilder WithNullMetadata()
    {
        _metadata = null;
        return this;
    }

    /// <summary>
    /// Sets Settings to null explicitly.
    /// </summary>
    public DictionaryTestEntityBuilder WithNullSettings()
    {
        _settings = null;
        return this;
    }

    /// <summary>
    /// Sets Metadata to an empty Dictionary.
    /// </summary>
    public DictionaryTestEntityBuilder WithEmptyMetadata()
    {
        _metadata = new Dictionary<string, string>();
        return this;
    }

    /// <summary>
    /// Sets Settings to an empty Dictionary.
    /// </summary>
    public DictionaryTestEntityBuilder WithEmptySettings()
    {
        _settings = new Dictionary<string, string>();
        return this;
    }

    /// <summary>
    /// Builds the DictionaryTestEntity with the configured values.
    /// Default: Only Id is set, all dictionaries are null.
    /// </summary>
    public DictionaryTestEntity Build()
    {
        return new DictionaryTestEntity
        {
            Id = _id,
            Metadata = _metadata,
            Settings = _settings
        };
    }
}
