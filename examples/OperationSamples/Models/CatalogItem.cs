using Oproto.FluentDynamoDb.Attributes;

namespace FluentDynamoDb.OperationSamples.Models;

/// <summary>
/// Represents a catalog item demonstrating non-key computed fields.
///
/// The Gsi1Pk property is a GSI partition key that is automatically computed
/// from the Category and Region source properties using "#" as a separator.
/// This enables efficient querying by category-region combinations without
/// requiring manual key composition.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): Item identifier</description></item>
/// <item><description>Sort Key (sk): Item metadata discriminator</description></item>
/// <item><description>GSI1 Partition Key (gsi1pk): Computed from "Category#Region"</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("Orders")]
[GenerateEntityProperty(Name = "CatalogItems")]
public partial class CatalogItem
{
    /// <summary>
    /// Gets or sets the partition key.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item category.
    /// This is a source property for the computed Gsi1Pk field.
    /// </summary>
    [DynamoDbAttribute("category")]
    [Extracted("Gsi1Pk", 0)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item region.
    /// This is a source property for the computed Gsi1Pk field.
    /// </summary>
    [DynamoDbAttribute("region")]
    [Extracted("Gsi1Pk", 1)]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the GSI partition key, computed from Category and Region.
    /// This is a non-key computed field: a GSI partition key computed from
    /// Category + Region using "#" as the separator.
    /// </summary>
    [GsiPartitionKey("category-region-index")]
    [DynamoDbAttribute("gsi1pk")]
    [Computed("Category", "Region", Separator = "#")]
    public string Gsi1Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the catalog item title.
    /// </summary>
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
}
