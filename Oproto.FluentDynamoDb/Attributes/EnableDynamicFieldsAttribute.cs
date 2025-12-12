namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Enables dynamic fields support for an entity, allowing capture of unmapped DynamoDB attributes.
/// </summary>
/// <remarks>
/// <para>
/// When applied to an entity class, the source generator will:
/// </para>
/// <list type="bullet">
/// <item><description>Generate a <c>DynamicFields</c> property of type <c>DynamicFieldCollection</c></description></item>
/// <item><description>Capture unmapped attributes during deserialization into the <c>DynamicFields</c> collection</description></item>
/// <item><description>Include dynamic fields when serializing the entity to DynamoDB</description></item>
/// </list>
/// <para>
/// This is useful for multi-tenant applications where end users can define custom fields,
/// or when working with items that have attributes not known at compile time.
/// </para>
/// <para>
/// The class must be declared as <c>partial</c> to allow the source generator to add the
/// <c>DynamicFields</c> property.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DynamoDbTable("Products")]
/// [EnableDynamicFields]
/// public partial class Product
/// {
///     [PartitionKey]
///     [DynamoDbAttribute("pk")]
///     public string Pk { get; set; } = string.Empty;
///     
///     [DynamoDbAttribute("name")]
///     public string Name { get; set; } = string.Empty;
/// }
/// 
/// // Access dynamic fields after retrieval
/// var product = await table.Products.Get(pk).GetItemAsync();
/// var customColor = product.DynamicFields.GetString("custom_color");
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EnableDynamicFieldsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether dynamic field values should be treated as sensitive data in logs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c> (the default), dynamic field values are redacted in logs while
    /// field names are still logged for debugging purposes.
    /// </para>
    /// <para>
    /// Set to <c>false</c> to include dynamic field values in logs. Only do this if you
    /// are certain that dynamic fields will not contain sensitive information.
    /// </para>
    /// </remarks>
    /// <value>
    /// <c>true</c> to redact dynamic field values in logs (default);
    /// <c>false</c> to include values in logs.
    /// </value>
    public bool SensitiveLogging { get; set; } = true;
}
