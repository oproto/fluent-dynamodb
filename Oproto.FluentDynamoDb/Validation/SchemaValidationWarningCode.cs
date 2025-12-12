namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Warning codes for non-critical schema validation differences.
/// </summary>
public enum SchemaValidationWarningCode
{
    // ========================================
    // Extra Items in DynamoDB (100-199)
    // ========================================
    
    /// <summary>
    /// The table has a GSI that is not defined in the entity metadata.
    /// </summary>
    UnexpectedGsi = 100,
    
    /// <summary>
    /// The table has an LSI that is not defined in the entity metadata.
    /// </summary>
    UnexpectedLsi = 101,
    
    /// <summary>
    /// The table has TTL enabled but the entity metadata does not define a TTL attribute.
    /// </summary>
    UnexpectedTtl = 102,
    
    // ========================================
    // Projection Warnings (200-299)
    // ========================================
    
    /// <summary>
    /// A projection model is recommended for an index with non-ALL projection (Relaxed mode).
    /// </summary>
    ProjectionModelRecommended = 200
}
