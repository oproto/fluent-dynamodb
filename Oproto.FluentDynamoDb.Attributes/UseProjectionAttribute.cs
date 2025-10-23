namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Enforces that queries on a Global Secondary Index (GSI) must use a specific projection model.
/// This is an opt-in validation for GSIs that project only specific attributes.
/// </summary>
/// <remarks>
/// When applied to a property marked with [GlobalSecondaryIndex], this attribute
/// indicates that the GSI projects only a subset of attributes and queries should
/// use the specified projection model type.
/// 
/// The source generator will:
/// - Generate a DynamoDbIndex&lt;TProjection&gt; property on the table class
/// - Automatically apply the projection expression to queries
/// - Validate at compile-time that the projection type is valid
/// 
/// <para><strong>Limitation:</strong> Currently, only ONE [UseProjection] attribute is supported per GSI.
/// If multiple entities define the same GSI with different [UseProjection] attributes,
/// the first one encountered will be used. To query the same GSI with different projections,
/// use the generic QueryAsync&lt;TResult&gt; override method on the generated index property.</para>
/// 
/// Example:
/// <code>
/// [DynamoDbAttribute("status_pk")]
/// [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
/// [UseProjection(typeof(TransactionSummary))]
/// public string StatusIndexPk { get; set; }
/// </code>
/// 
/// This will generate:
/// <code>
/// public DynamoDbIndex&lt;TransactionSummary&gt; StatusIndex => 
///     new DynamoDbIndex&lt;TransactionSummary&gt;(
///         this, 
///         "StatusIndex", 
///         "id, amount, status, entity_type");
/// </code>
/// 
/// To use an alternative projection on the same GSI:
/// <code>
/// // Use default projection (TransactionSummary)
/// var summaries = await table.StatusIndex.QueryAsync(q => q.Where("status = :s"));
/// 
/// // Override with different projection
/// var minimal = await table.StatusIndex.QueryAsync&lt;MinimalTransaction&gt;(q => q.Where("status = :s"));
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class UseProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the projection model type that must be used when querying this GSI.
    /// </summary>
    public Type ProjectionType { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="UseProjectionAttribute"/> class.
    /// </summary>
    /// <param name="projectionType">The projection model type that must be used when querying this GSI.</param>
    public UseProjectionAttribute(Type projectionType)
    {
        ProjectionType = projectionType;
    }
}
