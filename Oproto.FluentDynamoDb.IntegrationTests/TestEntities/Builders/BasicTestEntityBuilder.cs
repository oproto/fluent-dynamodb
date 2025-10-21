namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities.Builders;

/// <summary>
/// Fluent builder for creating BasicTestEntity instances with sensible defaults.
/// </summary>
public class BasicTestEntityBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string? _sortKey;
    private string? _name;
    private int? _age;
    private string? _email;
    private bool? _isActive;
    private DateTime? _createdAt;
    private decimal? _score;

    /// <summary>
    /// Sets the partition key (Id) for the entity.
    /// </summary>
    public BasicTestEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the sort key for the entity.
    /// </summary>
    public BasicTestEntityBuilder WithSortKey(string sortKey)
    {
        _sortKey = sortKey;
        return this;
    }

    /// <summary>
    /// Sets the Name property.
    /// </summary>
    public BasicTestEntityBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the Age property.
    /// </summary>
    public BasicTestEntityBuilder WithAge(int age)
    {
        _age = age;
        return this;
    }

    /// <summary>
    /// Sets the Email property.
    /// </summary>
    public BasicTestEntityBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the IsActive property.
    /// </summary>
    public BasicTestEntityBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }

    /// <summary>
    /// Sets the CreatedAt property.
    /// </summary>
    public BasicTestEntityBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    /// <summary>
    /// Sets the Score property.
    /// </summary>
    public BasicTestEntityBuilder WithScore(decimal score)
    {
        _score = score;
        return this;
    }

    /// <summary>
    /// Sets default values for common test scenarios.
    /// </summary>
    public BasicTestEntityBuilder WithDefaults()
    {
        _name = "Test User";
        _age = 30;
        _email = "test@example.com";
        _isActive = true;
        _createdAt = DateTime.UtcNow;
        _score = 100.0m;
        return this;
    }

    /// <summary>
    /// Builds the BasicTestEntity with the configured values.
    /// Default: Only Id is set, all other properties are null.
    /// </summary>
    public BasicTestEntity Build()
    {
        return new BasicTestEntity
        {
            Id = _id,
            SortKey = _sortKey,
            Name = _name,
            Age = _age,
            Email = _email,
            IsActive = _isActive,
            CreatedAt = _createdAt,
            Score = _score
        };
    }
}
