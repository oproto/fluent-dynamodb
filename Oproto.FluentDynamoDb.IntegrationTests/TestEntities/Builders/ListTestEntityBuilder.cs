namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities.Builders;

/// <summary>
/// Fluent builder for creating ListTestEntity instances with sensible defaults.
/// </summary>
public class ListTestEntityBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private List<string>? _itemIds;
    private List<int>? _quantities;
    private List<decimal>? _prices;

    /// <summary>
    /// Sets the partition key (Id) for the entity.
    /// </summary>
    public ListTestEntityBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the ItemIds property with the specified string values.
    /// </summary>
    public ListTestEntityBuilder WithItemIds(params string[] ids)
    {
        _itemIds = new List<string>(ids);
        return this;
    }

    /// <summary>
    /// Sets the Quantities property with the specified integer values.
    /// </summary>
    public ListTestEntityBuilder WithQuantities(params int[] quantities)
    {
        _quantities = new List<int>(quantities);
        return this;
    }

    /// <summary>
    /// Sets the Prices property with the specified decimal values.
    /// </summary>
    public ListTestEntityBuilder WithPrices(params decimal[] prices)
    {
        _prices = new List<decimal>(prices);
        return this;
    }

    /// <summary>
    /// Sets ItemIds to null explicitly.
    /// </summary>
    public ListTestEntityBuilder WithNullItemIds()
    {
        _itemIds = null;
        return this;
    }

    /// <summary>
    /// Sets Quantities to null explicitly.
    /// </summary>
    public ListTestEntityBuilder WithNullQuantities()
    {
        _quantities = null;
        return this;
    }

    /// <summary>
    /// Sets Prices to null explicitly.
    /// </summary>
    public ListTestEntityBuilder WithNullPrices()
    {
        _prices = null;
        return this;
    }

    /// <summary>
    /// Sets ItemIds to an empty List.
    /// </summary>
    public ListTestEntityBuilder WithEmptyItemIds()
    {
        _itemIds = new List<string>();
        return this;
    }

    /// <summary>
    /// Sets Quantities to an empty List.
    /// </summary>
    public ListTestEntityBuilder WithEmptyQuantities()
    {
        _quantities = new List<int>();
        return this;
    }

    /// <summary>
    /// Sets Prices to an empty List.
    /// </summary>
    public ListTestEntityBuilder WithEmptyPrices()
    {
        _prices = new List<decimal>();
        return this;
    }

    /// <summary>
    /// Builds the ListTestEntity with the configured values.
    /// Default: Only Id is set, all lists are null.
    /// </summary>
    public ListTestEntity Build()
    {
        return new ListTestEntity
        {
            Id = _id,
            ItemIds = _itemIds,
            Quantities = _quantities,
            Prices = _prices
        };
    }
}
