namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the sort key for a DynamoDB table.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SortKeyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an optional prefix for the sort key value.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the separator used when combining key components.
    /// Default is "#".
    /// </summary>
    public string? Separator { get; set; } = "#";
}