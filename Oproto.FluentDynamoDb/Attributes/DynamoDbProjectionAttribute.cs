namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a class as a projection model for a DynamoDB entity.
/// The source generator will create projection expressions and mapping code.
/// </summary>
/// <remarks>
/// Projection models define a subset of properties from a source entity,
/// allowing you to retrieve only the data you need from DynamoDB queries.
/// This reduces data transfer and read capacity consumption.
/// 
/// The projection class must be declared as partial to allow the source generator
/// to add the generated mapping code.
/// 
/// Example:
/// <code>
/// [DynamoDbProjection(typeof(Transaction))]
/// public partial class TransactionSummary
/// {
///     public string Id { get; set; }
///     public decimal Amount { get; set; }
///     public DateTime CreatedDate { get; set; }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DynamoDbProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the source entity type that this projection derives from.
    /// </summary>
    public Type SourceEntityType { get; }
    
    /// <summary>
    /// Gets or sets the properties to include in the projection.
    /// If null or empty, all properties on the projection model are included.
    /// </summary>
    /// <remarks>
    /// This is typically not needed as the source generator will automatically
    /// include all properties defined on the projection class. Use this only
    /// if you need to explicitly control which properties are projected.
    /// </remarks>
    public string[]? IncludeProperties { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamoDbProjectionAttribute"/> class.
    /// </summary>
    /// <param name="sourceEntityType">The source entity type that this projection derives from.</param>
    public DynamoDbProjectionAttribute(Type sourceEntityType)
    {
        SourceEntityType = sourceEntityType;
    }
}
