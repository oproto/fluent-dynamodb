using Oproto.FluentDynamoDb.Attributes;

namespace DynamicFieldsDemo.Entities;

/// <summary>
/// Represents a product in a multi-tenant e-commerce system.
/// 
/// This entity demonstrates dynamic fields support, allowing tenants to define
/// custom attributes for their products without schema changes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Dynamic Fields Use Case:</strong>
/// </para>
/// <para>
/// In a multi-tenant SaaS application, different tenants may need different product attributes:
/// </para>
/// <list type="bullet">
/// <item><description>A clothing store might need "size", "color", "material"</description></item>
/// <item><description>An electronics store might need "warranty_months", "voltage", "weight_kg"</description></item>
/// <item><description>A food store might need "expiry_date", "calories", "allergens"</description></item>
/// </list>
/// <para>
/// With dynamic fields, all these custom attributes can be stored and retrieved without
/// modifying the entity class or database schema.
/// </para>
/// <para>
/// <strong>Attribute Usage:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DynamoDbTableAttribute"/> - Specifies the DynamoDB table name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="EnableDynamicFieldsAttribute"/> - Enables capture of unmapped attributes.
/// The source generator will add a <c>DynamicFields</c> property to access custom fields.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScannableAttribute"/> - Enables Scan() operations for this demo.
/// </description>
/// </item>
/// </list>
/// </remarks>
[DynamoDbTable("products", IsDefault = true)]
[EnableDynamicFields]
[Scannable]
[GenerateEntityProperty(Name = "Products")]
public partial class Product
{
    /// <summary>
    /// Gets or sets the partition key (product ID with prefix).
    /// Format: "PRODUCT#{productId}"
    /// </summary>
    [PartitionKey(Prefix = "PRODUCT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key (metadata marker).
    /// For this simple example, we use a constant "META" value.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "META";

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product price.
    /// </summary>
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the product category.
    /// </summary>
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the product was created.
    /// </summary>
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
