using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the partition key for a DynamoDB table.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PartitionKeyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an optional prefix for the partition key value.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the separator used when combining key components.
    /// Default is "#".
    /// </summary>
    public string? Separator { get; set; } = "#";
}
