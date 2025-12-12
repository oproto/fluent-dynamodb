namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a Local Secondary Index attribute on a property.
/// LSIs share the same partition key as the base table but have a different sort key.
/// </summary>
internal class LocalSecondaryIndexModel
{
    /// <summary>
    /// Gets or sets the name of the Local Secondary Index.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
}
