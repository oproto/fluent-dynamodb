using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

namespace TodoList.Entities;

/// <summary>
/// Represents a todo item in the todo list application.
/// 
/// This entity demonstrates basic CRUD operations with FluentDynamoDb using a simple
/// single-key table design. The <see cref="ScannableAttribute"/> is applied because
/// todo lists are typically small datasets where scan operations are acceptable.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Attribute Usage:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DynamoDbEntityAttribute"/> - Marks this class for source generation of
/// DynamoDB mapping code (ToDynamoDb/FromDynamoDb methods).
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="DynamoDbTableAttribute"/> - Specifies the DynamoDB table name. The IsDefault=true
/// indicates this is the primary entity for the table.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScannableAttribute"/> - Enables Scan() operations on the table. Use sparingly
/// as scans read every item in the table. Appropriate here because todo lists are small.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PartitionKeyAttribute"/> - Marks the Id property as the partition key.
/// For single-key tables, this is the only key attribute needed.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="DynamoDbAttributeAttribute"/> - Maps C# properties to DynamoDB attribute names.
/// The attribute name parameter specifies the name used in DynamoDB.
/// </description>
/// </item>
/// </list>
/// </remarks>
[DynamoDbEntity]
[DynamoDbTable("todo-items", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "TodoItems")]
public partial class TodoItem : IDynamoDbEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the todo item.
    /// This serves as the partition key for the DynamoDB table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the todo item.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the todo item has been completed.
    /// </summary>
    [DynamoDbAttribute("isComplete")]
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the todo item was created.
    /// </summary>
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the todo item was completed.
    /// Null if the item has not been completed.
    /// </summary>
    [DynamoDbAttribute("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
