using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a class as a DynamoDB entity and specifies the table name.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DynamoDbTableAttribute : Attribute
{
    /// <summary>
    /// Gets the DynamoDB table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets the type of the table class to use for this entity.
    /// When specified, the source generator will use this type as the table class
    /// instead of generating a new one.
    /// </summary>
    public Type? TableType { get; }

    /// <summary>
    /// Gets or sets the property name containing the discriminator (e.g., "entity_type", "SK", "PK").
    /// If null, no discriminator validation is performed.
    /// </summary>
    /// <remarks>
    /// The discriminator property is used to identify which entity type a DynamoDB item represents
    /// when multiple entity types share the same table. Common patterns:
    /// <list type="bullet">
    /// <item><description>"entity_type" - Dedicated attribute for entity type</description></item>
    /// <item><description>"SK" - Sort key contains entity type (e.g., "USER#123")</description></item>
    /// <item><description>"PK" - Partition key contains entity type</description></item>
    /// </list>
    /// </remarks>
    public string? DiscriminatorProperty { get; set; }

    /// <summary>
    /// Gets or sets the exact value to match for this entity type.
    /// Mutually exclusive with <see cref="DiscriminatorPattern"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [DynamoDbTable("MyTable", 
    ///     DiscriminatorProperty = "entity_type",
    ///     DiscriminatorValue = "USER")]
    /// </code>
    /// </example>
    public string? DiscriminatorValue { get; set; }

    /// <summary>
    /// Gets or sets a pattern to match for this entity type (supports * wildcard).
    /// Mutually exclusive with <see cref="DiscriminatorValue"/>.
    /// </summary>
    /// <remarks>
    /// Pattern matching supports the * wildcard character:
    /// <list type="bullet">
    /// <item><description>"USER#*" - Matches any value starting with "USER#"</description></item>
    /// <item><description>"*#USER#*" - Matches any value containing "#USER#"</description></item>
    /// <item><description>"*USER" - Matches any value ending with "USER"</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// [DynamoDbTable("MyTable",
    ///     DiscriminatorProperty = "SK",
    ///     DiscriminatorPattern = "USER#*")]
    /// </code>
    /// </example>
    public string? DiscriminatorPattern { get; set; }

    /// <summary>
    /// Gets or sets an optional entity discriminator for multi-type tables.
    /// </summary>
    /// <remarks>
    /// <strong>Legacy property for backward compatibility.</strong>
    /// Equivalent to setting <c>DiscriminatorProperty = "entity_type"</c> and <c>DiscriminatorValue</c>.
    /// New code should use <see cref="DiscriminatorProperty"/> and <see cref="DiscriminatorValue"/> instead.
    /// </remarks>
    [Obsolete("Use DiscriminatorProperty and DiscriminatorValue instead. This property will be removed in a future version.")]
    public string? EntityDiscriminator { get; set; }

    /// <summary>
    /// Gets or sets whether this entity is the default entity for the table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When multiple entities share the same table name, one entity must be marked as the default.
    /// The default entity is used for table-level operations (e.g., <c>table.Get()</c>, <c>table.Query()</c>)
    /// and provides the generic type parameter for the table class.
    /// </para>
    /// <para>
    /// If only one entity is assigned to a table, it is automatically treated as the default
    /// and this property does not need to be set.
    /// </para>
    /// <para>
    /// If multiple entities share a table and no default is specified, or if multiple entities
    /// are marked as default, the source generator will emit a compile-time error.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para><strong>Single-table design with default entity:</strong></para>
    /// <code>
    /// [DynamoDbTable(TableName = "MyApp", IsDefault = true)]
    /// public class Order
    /// {
    ///     // This is the default entity
    ///     // Table-level operations use Order type: table.Get(), table.Query()
    /// }
    /// 
    /// [DynamoDbTable(TableName = "MyApp")]
    /// public class OrderLine
    /// {
    ///     // Access via entity accessor: table.OrderLines.Get()
    /// }
    /// </code>
    /// </example>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets the namespace for the generated table class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, the generated table class is placed in the same namespace as the entity class.
    /// Use this property to specify a different namespace for the generated table class.
    /// </para>
    /// <para>
    /// This is useful when you want to organize your generated code according to your project's
    /// namespace conventions, separating entity definitions from table access classes.
    /// </para>
    /// <para>
    /// Note: The entity class itself remains in its declared namespace. Only the generated
    /// table class is placed in the specified namespace.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para><strong>Custom namespace for generated table class:</strong></para>
    /// <code>
    /// // Entity in MyApp.Domain namespace
    /// namespace MyApp.Domain;
    /// 
    /// [DynamoDbTable("Orders", Namespace = "MyApp.Infrastructure.DynamoDb")]
    /// public partial class Order
    /// {
    ///     // Entity properties...
    /// }
    /// 
    /// // Generated table class will be in MyApp.Infrastructure.DynamoDb namespace:
    /// // namespace MyApp.Infrastructure.DynamoDb;
    /// // public partial class OrdersTable : IDynamoDbTable { ... }
    /// </code>
    /// </example>
    public string? Namespace { get; set; }

    /// <summary>
    /// Initializes a new instance of the DynamoDbTableAttribute class.
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    public DynamoDbTableAttribute(string tableName)
    {
        TableName = tableName;
        TableType = null;
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbTableAttribute class with a type-safe table class reference.
    /// </summary>
    /// <param name="tableType">The type of the table class to use for this entity.</param>
    /// <remarks>
    /// <para>
    /// Use this constructor when you want to reference an existing partial table class
    /// instead of having the source generator create a new one. This provides compile-time
    /// safety and refactoring support for table class references.
    /// </para>
    /// <para>
    /// The referenced type must be declared as a partial class. The source generator will
    /// emit a compile-time error if the type is not partial.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define a partial table class
    /// public partial class MyTable { }
    /// 
    /// // Reference it from an entity
    /// [DynamoDbTable(typeof(MyTable))]
    /// public partial class Order
    /// {
    ///     // Entity properties...
    /// }
    /// </code>
    /// </example>
    public DynamoDbTableAttribute(Type tableType)
    {
        TableType = tableType ?? throw new ArgumentNullException(nameof(tableType));
        TableName = string.Empty; // Will be derived from the table type
    }
}
