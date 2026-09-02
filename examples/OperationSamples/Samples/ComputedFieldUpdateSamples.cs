using FluentDynamoDb.OperationSamples.Models;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace FluentDynamoDb.OperationSamples.Samples;

/// <summary>
/// Demonstrates computed field updates using the source-generated update model.
///
/// CatalogItem has a non-key computed field (Gsi1Pk) that is a GSI partition key
/// computed from Category and Region with "#" separator. When updating, setting
/// the source properties in the update model triggers automatic recomputation of
/// the computed field in the generated SET expression.
/// </summary>
/// <remarks>
/// <para><strong>Key Behaviors:</strong></para>
/// <list type="bullet">
/// <item><description>
/// PK, SK, and extracted properties targeting key fields are excluded from the
/// generated update model. Only non-key computed fields and regular properties
/// are available for assignment in update expressions.
/// </description></item>
/// <item><description>
/// Setting only a subset of source properties for a computed field produces
/// diagnostic error FDDB072. All source properties must be assigned together.
/// </description></item>
/// </list>
/// </remarks>
public static class ComputedFieldUpdateSamples
{
    /// <summary>
    /// Demonstrates updating all source properties of a non-key computed field.
    ///
    /// Setting both Category and Region in the update model triggers the expression
    /// translator to produce a SET expression targeting gsi1pk with the concatenated
    /// value, in addition to setting each source property individually.
    /// </summary>
    public static async Task UpdateComputedFieldAsync(OrdersTable table, string pk, string sk)
    {
        // Non-key computed fields are recomputed automatically from source property values.
        // Setting Category and Region in the update model triggers the expression translator
        // to produce a SET expression targeting gsi1pk with the concatenated value.
        //
        // Individual source properties are also persisted to their own DynamoDB attributes:
        //   category = "electronics"
        //   region = "us-west-2"
        //
        // PK, SK, and extracted properties targeting key fields are excluded from the
        // generated update model — they are not available for assignment in update expressions.
        //
        // Setting only a subset of source properties (e.g., Category without Region) produces
        // diagnostic FDDB072, requiring all source properties to be assigned together.
        await table.CatalogItems.Update(pk, sk)
            .Set(x => new CatalogItemUpdateModel
            {
                Category = "electronics",
                Region = "us-west-2"
            })
            .UpdateAsync();

        // Result: gsi1pk = "electronics#us-west-2", category = "electronics", region = "us-west-2"
    }

    /// <summary>
    /// Demonstrates updating source properties alongside other non-computed properties.
    /// Both the computed field recomputation and the regular property update are included
    /// in the same SET expression.
    /// </summary>
    public static async Task UpdateComputedFieldWithTitleAsync(OrdersTable table, string pk, string sk)
    {
        // You can set source properties and regular properties in the same update expression.
        // The computed field (gsi1pk) is recomputed from the source properties, while
        // non-computed properties (title) are set directly.
        await table.CatalogItems.Update(pk, sk)
            .Set(x => new CatalogItemUpdateModel
            {
                Category = "books",
                Region = "eu-west-1",
                Title = "Introduction to DynamoDB"
            })
            .UpdateAsync();

        // Result: gsi1pk = "books#eu-west-1", category = "books", region = "eu-west-1", title = "Introduction to DynamoDB"
    }
}
