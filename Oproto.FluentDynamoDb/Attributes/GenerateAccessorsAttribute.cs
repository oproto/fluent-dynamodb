using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Controls the generation of DynamoDB operation methods on entity accessor classes.
/// This attribute can be applied multiple times to configure different operations independently.
/// </summary>
/// <remarks>
/// <para>
/// By default, all operations (Get, Query, Scan, Put, Delete, Update) are generated as public methods.
/// Use this attribute to:
/// <list type="bullet">
/// <item><description>Hide specific operations by setting <see cref="Generate"/> to <c>false</c></description></item>
/// <item><description>Change visibility of operations using <see cref="Modifier"/></description></item>
/// <item><description>Configure multiple operations at once using the <see cref="DynamoDbOperation"/> flags</description></item>
/// </list>
/// </para>
/// <para>
/// This attribute is repeatable (<c>AllowMultiple = true</c>), allowing fine-grained control
/// over each operation. However, configuring the same operation multiple times will result
/// in a compile-time error.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Make all operations internal:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
/// public class Order
/// {
///     // All operations (Get, Query, Scan, Put, Delete, Update) generated as internal
/// }
/// </code>
/// 
/// <para><strong>Disable delete operation:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
/// public class ReadOnlyEntity
/// {
///     // Get, Query, Scan, Put, Update generated as public
///     // Delete operation not generated
/// }
/// </code>
/// 
/// <para><strong>Mixed visibility - internal writes, public reads:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateAccessors(Operations = TableOperation.Put | TableOperation.Delete | TableOperation.Update, 
///                    Modifier = AccessModifier.Internal)]
/// [GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query | TableOperation.Scan,
///                    Modifier = AccessModifier.Public)]
/// public class Order
/// {
///     // Get, Query, Scan are public
///     // Put, Delete, Update are internal
/// }
/// 
/// // Create custom public methods in partial class:
/// public partial class MyAppTable
/// {
///     public async Task CreateOrderAsync(Order order)
///     {
///         // Validate business rules
///         ValidateOrder(order);
///         
///         // Call internal generated method
///         await Orders.Put(order).ExecuteAsync();
///     }
/// }
/// </code>
/// 
/// <para><strong>Query-only entity:</strong></para>
/// <code>
/// [DynamoDbTable(TableName = "MyApp")]
/// [GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query)]
/// [GenerateAccessors(Operations = TableOperation.Put | TableOperation.Delete | TableOperation.Update,
///                    Generate = false)]
/// public class ReadOnlyView
/// {
///     // Only Get and Query operations generated
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GenerateAccessorsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets which DynamoDB operations this configuration applies to.
    /// Default is <see cref="TableOperation.All"/>.
    /// </summary>
    /// <remarks>
    /// Use the <see cref="TableOperation"/> flags enumeration to specify one or more operations.
    /// Multiple operations can be combined using the bitwise OR operator (|).
    /// </remarks>
    /// <example>
    /// <code>
    /// // Configure a single operation
    /// [GenerateAccessors(Operations = TableOperation.Get)]
    /// 
    /// // Configure multiple operations
    /// [GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query)]
    /// 
    /// // Configure all operations
    /// [GenerateAccessors(Operations = TableOperation.All)]
    /// </code>
    /// </example>
    public TableOperation Operations { get; set; } = TableOperation.All;

    /// <summary>
    /// Gets or sets whether to generate the specified operations.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Set to <c>false</c> to prevent generation of the specified operations.
    /// Useful for creating read-only or write-only entities, or for hiding operations
    /// that should only be accessed through custom methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Disable delete operation
    /// [GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
    /// </code>
    /// </example>
    public bool Generate { get; set; } = true;

    /// <summary>
    /// Gets or sets the visibility modifier for the generated operation methods.
    /// Default is <see cref="AccessModifier.Public"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="AccessModifier.Internal"/> or <see cref="AccessModifier.Private"/>
    /// to hide implementation details and expose custom public methods through partial classes.
    /// This enables you to add validation, logging, or other business logic before calling
    /// the generated DynamoDB operations.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Make write operations internal
    /// [GenerateAccessors(
    ///     Operations = TableOperation.Put | TableOperation.Delete | TableOperation.Update,
    ///     Modifier = AccessModifier.Internal)]
    /// </code>
    /// </example>
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
