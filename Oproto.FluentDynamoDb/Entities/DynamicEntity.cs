using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// A schema-less entity where all attributes are stored in DynamicFields.
/// Use with DynamicTable for accessing tables without defining entity classes.
/// </summary>
/// <remarks>
/// <para>
/// DynamicEntity enables schema-less access to any DynamoDB table. All attributes
/// from the DynamoDB item are stored in the <see cref="DynamicFields"/> collection,
/// which provides typed accessors for common types.
/// </para>
/// <para>
/// This entity type is useful for:
/// <list type="bullet">
/// <item><description>Exploring unknown table schemas</description></item>
/// <item><description>Building migration tools</description></item>
/// <item><description>Working with tables that have no fixed schema</description></item>
/// <item><description>Accessing tables without defining entity classes</description></item>
/// </list>
/// </para>
/// <para>
/// When using DynamicEntity with lambda expressions, the expression translator
/// allows DynamicFields indexer access in key conditions without validation errors.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Query using DynamicEntity
/// var results = await dynamicTable.Query()
///     .Where(x => x.DynamicFields["pk"] == "USER#123")
///     .ExecuteAsync();
/// 
/// // Access fields from results
/// foreach (var entity in results)
/// {
///     var name = entity.DynamicFields.GetString("name");
///     var age = entity.DynamicFields.GetInt("age");
/// }
/// </code>
/// </example>
public sealed class DynamicEntity : IDynamoDbEntity
{
    /// <summary>
    /// All attributes from the DynamoDB item.
    /// </summary>
    /// <remarks>
    /// Use the typed getter methods (GetString, GetInt, etc.) for runtime access.
    /// Use the indexer syntax in lambda expressions for filter and condition expressions.
    /// </remarks>
    public DynamicFieldCollection DynamicFields { get; set; } = new();


    /// <summary>
    /// Converts a DynamicEntity instance to a DynamoDB AttributeValue dictionary.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <param name="options">Optional configuration options. Not used for DynamicEntity.</param>
    /// <returns>A dictionary of attribute names to AttributeValue objects.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity is not a DynamicEntity.</exception>
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        if (entity is not DynamicEntity dynamicEntity)
            throw new InvalidOperationException($"Expected DynamicEntity but received {typeof(TSelf).Name}");

        return dynamicEntity.DynamicFields.ToDictionary();
    }

    /// <summary>
    /// Creates a DynamicEntity instance from a single DynamoDB item.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="item">The DynamoDB item as an AttributeValue dictionary.</param>
    /// <param name="options">Optional configuration options. Not used for DynamicEntity.</param>
    /// <returns>A DynamicEntity with all attributes in the DynamicFields collection.</returns>
    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        var entity = new DynamicEntity
        {
            DynamicFields = new DynamicFieldCollection(new Dictionary<string, AttributeValue>(item))
        };
        entity.DynamicFields.StartTrackingChanges();
        return (TSelf)(object)entity;
    }

    /// <summary>
    /// Creates a DynamicEntity instance from multiple DynamoDB items.
    /// For DynamicEntity, this uses only the first item since DynamicEntity doesn't support multi-item entities.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="items">The collection of DynamoDB items.</param>
    /// <param name="options">Optional configuration options. Not used for DynamicEntity.</param>
    /// <returns>A DynamicEntity with attributes from the first item in the DynamicFields collection.</returns>
    /// <exception cref="ArgumentException">Thrown when the items collection is empty.</exception>
    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        if (items.Count == 0)
            throw new ArgumentException("Items collection cannot be empty", nameof(items));

        // DynamicEntity doesn't support multi-item entities, use first item
        return FromDynamoDb<TSelf>(items[0], options);
    }

    /// <summary>
    /// Gets the partition key value from a DynamoDB item.
    /// </summary>
    /// <param name="item">The DynamoDB item.</param>
    /// <returns>Always throws NotSupportedException as DynamicEntity requires explicit key specification.</returns>
    /// <exception cref="NotSupportedException">Always thrown. DynamicEntity requires explicit key specification via DynamicTableKeyOptions.</exception>
    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        throw new NotSupportedException(
            "DynamicEntity requires explicit key specification. " +
            "Use DynamicTableKeyOptions to configure the key schema when creating a DynamicTable.");
    }

    /// <summary>
    /// Determines whether a DynamoDB item matches this entity type.
    /// For DynamicEntity, all items match since it's schema-less.
    /// </summary>
    /// <param name="item">The DynamoDB item to check.</param>
    /// <returns>Always returns true since DynamicEntity accepts any item.</returns>
    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;

    /// <summary>
    /// Gets whether this entity type requires write operations within a transaction.
    /// DynamicEntity does not require transactions.
    /// </summary>
    public static bool RequiresWriteTransaction => false;

    /// <summary>
    /// Gets the entity metadata for DynamicEntity.
    /// </summary>
    /// <returns>Metadata indicating this is a dynamic entity that should skip key validation.</returns>
    public static EntityMetadata GetEntityMetadata() => new EntityMetadata
    {
        TableName = string.Empty, // Set at runtime via DynamicTable
        EntityDiscriminator = null,
        Properties = Array.Empty<PropertyMetadata>(),
        Indexes = Array.Empty<IndexMetadata>(),
        Relationships = Array.Empty<RelationshipMetadata>(),
        IsMultiItemEntity = false,
        RequiresWriteTransaction = false,
        PartitionKeyAttributeName = string.Empty, // Set at runtime via DynamicTableKeyOptions
        PartitionKeyAttributeType = "S",
        SortKeyAttributeName = null,
        SortKeyAttributeType = null,
        TtlAttributeName = null,
        IsDynamicEntity = true // Key flag for expression translator
    };
}
