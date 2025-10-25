using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Controls the generation of entity accessor properties on the table class.
/// Apply this attribute to an entity class to customize how its accessor property is generated.
/// </summary>
/// <remarks>
/// <para>
/// When multiple entities share the same DynamoDB table, the source generator creates
/// entity-specific accessor properties (e.g., <c>table.Orders</c>, <c>table.OrderLines</c>).
/// This attribute allows you to customize the name, visibility, and whether the accessor
/// is generated at all.
/// </para>
/// <para>
/// By default, if this attribute is not present, the generator will create a public
/// accessor property using the pluralized entity class name.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Custom accessor name:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateEntityProperty(Name = "AllOrders")]
/// public class Order
/// {
///     // Generates: public OrderAccessor AllOrders { get; }
/// }
/// </code>
/// 
/// <para><strong>Internal visibility for implementation hiding:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateEntityProperty(Modifier = AccessModifier.Internal)]
/// public class Order
/// {
///     // Generates: internal OrderAccessor Orders { get; }
/// }
/// 
/// // In a partial class, create custom public methods:
/// public partial class MyAppTable
/// {
///     public async Task&lt;Order&gt; GetOrderByIdAsync(string orderId)
///     {
///         // Call internal generated accessor
///         return await Orders.Get()
///             .WithKey("PK", $"ORDER#{orderId}")
///             .ExecuteAsync();
///     }
/// }
/// </code>
/// 
/// <para><strong>Disable accessor generation:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateEntityProperty(Generate = false)]
/// public class InternalEntity
/// {
///     // No accessor property generated - entity metadata still available
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class GenerateEntityPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the custom name for the entity accessor property.
    /// If not specified, the generator uses the pluralized entity class name.
    /// </summary>
    /// <example>
    /// <code>
    /// [GenerateEntityProperty(Name = "AllOrders")]
    /// public class Order { }
    /// // Generates: public OrderAccessor AllOrders { get; }
    /// </code>
    /// </example>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether to generate the entity accessor property.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Set to <c>false</c> to prevent generation of the accessor property while still
    /// including the entity's metadata in the table. Useful for internal entities that
    /// should not be directly accessible through the table API.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEntityProperty(Generate = false)]
    /// public class InternalEntity { }
    /// // No accessor property generated
    /// </code>
    /// </example>
    public bool Generate { get; set; } = true;

    /// <summary>
    /// Gets or sets the visibility modifier for the generated accessor property.
    /// Default is <see cref="AccessModifier.Public"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="AccessModifier.Internal"/> or <see cref="AccessModifier.Private"/>
    /// to hide implementation details and expose custom public methods through partial classes.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateEntityProperty(Modifier = AccessModifier.Internal)]
    /// public class Order { }
    /// // Generates: internal OrderAccessor Orders { get; }
    /// </code>
    /// </example>
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
