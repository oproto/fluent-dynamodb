using Oproto.FluentDynamoDb.Attributes;

namespace InvoiceManager.Entities;

/// <summary>
/// Represents a customer in the invoice management system.
/// 
/// This entity is part of a single-table design where multiple entity types
/// (Customer, Invoice, InvoiceLine) share the same DynamoDB table. The key design
/// uses hierarchical composite keys to enable efficient access patterns.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Single-Table Design:</strong>
/// </para>
/// <para>
/// In DynamoDB, single-table design stores multiple entity types in one table,
/// using composite keys to distinguish between them. This enables:
/// </para>
/// <list type="bullet">
/// <item><description>Fetching related entities in a single query</description></item>
/// <item><description>Atomic transactions across entity types</description></item>
/// <item><description>Reduced operational overhead (one table to manage)</description></item>
/// </list>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "CUSTOMER#{customerId}" - groups all data for a customer</description></item>
/// <item><description>Sort Key (sk): "PROFILE" - identifies this as the customer profile record</description></item>
/// </list>
/// <para>
/// This design allows querying all customer data (profile, invoices, line items)
/// with a single Query operation using the partition key.
/// </para>
/// </remarks>
[DynamoDbTable("invoices")]
[GenerateEntityProperty(Name = "Customers")]
[Scannable]
public partial class Customer
{
    /// <summary>
    /// Gets or sets the partition key in format "CUSTOMER#{customerId}".
    /// This groups all data for a single customer together.
    /// </summary>
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key. For customers, this is always "PROFILE".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique customer identifier.
    /// </summary>
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    [DynamoDbAttribute("customerName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer's email address.
    /// </summary>
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The sort key value for customer profile records.
    /// </summary>
    public const string ProfileSk = "PROFILE";
}
