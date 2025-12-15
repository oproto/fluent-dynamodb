using Amazon.DynamoDBv2;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Options for configuring a DynamicTable's key schema.
/// Use this to enable typed key methods (Get, Delete, Update) on DynamicTable.
/// </summary>
/// <remarks>
/// When key options are configured, DynamicTable provides typed overloads for key-based operations
/// that accept string or numeric parameters instead of raw AttributeValue objects.
/// </remarks>
/// <example>
/// <code>
/// // String partition key only
/// var keyOptions = new DynamicTableKeyOptions
/// {
///     PartitionKeyName = "pk",
///     PartitionKeyType = ScalarAttributeType.S
/// };
/// 
/// // String partition key with numeric sort key
/// var keyOptions = new DynamicTableKeyOptions
/// {
///     PartitionKeyName = "pk",
///     PartitionKeyType = ScalarAttributeType.S,
///     SortKeyName = "sk",
///     SortKeyType = ScalarAttributeType.N
/// };
/// </code>
/// </example>
public class DynamicTableKeyOptions
{
    /// <summary>
    /// Gets or sets the name of the partition key attribute.
    /// </summary>
    /// <value>The partition key attribute name. Defaults to "pk".</value>
    public string PartitionKeyName { get; set; } = "pk";

    /// <summary>
    /// Gets or sets the DynamoDB scalar type of the partition key.
    /// </summary>
    /// <value>The partition key type. Defaults to <see cref="ScalarAttributeType.S"/> (String).</value>
    public ScalarAttributeType PartitionKeyType { get; set; } = ScalarAttributeType.S;

    /// <summary>
    /// Gets or sets the name of the sort key attribute.
    /// </summary>
    /// <value>The sort key attribute name, or null if the table has no sort key.</value>
    public string? SortKeyName { get; set; }

    /// <summary>
    /// Gets or sets the DynamoDB scalar type of the sort key.
    /// </summary>
    /// <value>The sort key type, or null if the table has no sort key.</value>
    public ScalarAttributeType? SortKeyType { get; set; }

    /// <summary>
    /// Gets a value indicating whether this key configuration includes a sort key.
    /// </summary>
    public bool HasSortKey => SortKeyName != null && SortKeyType != null;
}
