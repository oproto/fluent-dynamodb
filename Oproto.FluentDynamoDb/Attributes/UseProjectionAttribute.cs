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
/// use the Query&lt;TResult&gt;() method with different type parameters on the generated index property.</para>
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
