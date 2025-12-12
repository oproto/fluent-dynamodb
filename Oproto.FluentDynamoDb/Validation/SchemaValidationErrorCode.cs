namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Error codes for schema validation failures.
/// </summary>
public enum SchemaValidationErrorCode
{
    // ========================================
    // Primary Key Errors (100-199)
    // ========================================
    
    /// <summary>
    /// The partition key attribute name in DynamoDB does not match the entity metadata.
    /// </summary>
    PartitionKeyNameMismatch = 100,
    
    /// <summary>
    /// The partition key attribute type (S, N, B) does not match the expected type.
    /// </summary>
    PartitionKeyTypeMismatch = 101,
    
    /// <summary>
    /// The entity metadata defines a sort key but the table does not have one.
    /// </summary>
    SortKeyMissing = 110,
    
    /// <summary>
    /// The table has a sort key but the entity metadata does not define one.
    /// </summary>
    SortKeyUnexpected = 111,
    
    /// <summary>
    /// The sort key attribute name in DynamoDB does not match the entity metadata.
    /// </summary>
    SortKeyNameMismatch = 112,
    
    /// <summary>
    /// The sort key attribute type (S, N, B) does not match the expected type.
    /// </summary>
    SortKeyTypeMismatch = 113,
    
    // ========================================
    // Global Secondary Index Errors (200-299)
    // ========================================
    
    /// <summary>
    /// A GSI defined in entity metadata does not exist on the table.
    /// </summary>
    GsiNotFound = 200,
    
    /// <summary>
    /// The GSI partition key attribute name does not match the entity metadata.
    /// </summary>
    GsiPartitionKeyNameMismatch = 201,
    
    /// <summary>
    /// The GSI partition key attribute type does not match the expected type.
    /// </summary>
    GsiPartitionKeyTypeMismatch = 202,
    
    /// <summary>
    /// The GSI sort key configuration does not match (missing, extra, or wrong attribute).
    /// </summary>
    GsiSortKeyMismatch = 210,
    
    // ========================================
    // Local Secondary Index Errors (300-399)
    // ========================================
    
    /// <summary>
    /// An LSI defined in entity metadata does not exist on the table.
    /// </summary>
    LsiNotFound = 300,
    
    /// <summary>
    /// The LSI sort key attribute name does not match the entity metadata.
    /// </summary>
    LsiSortKeyNameMismatch = 310,
    
    /// <summary>
    /// The LSI sort key attribute type does not match the expected type.
    /// </summary>
    LsiSortKeyTypeMismatch = 311,
    
    // ========================================
    // TTL Errors (400-499)
    // ========================================
    
    /// <summary>
    /// The entity metadata defines a TTL attribute but TTL is not enabled on the table.
    /// </summary>
    TtlNotEnabled = 400,
    
    /// <summary>
    /// The TTL attribute name in DynamoDB does not match the entity metadata.
    /// </summary>
    TtlAttributeNameMismatch = 401,
    
    // ========================================
    // Projection Errors (500-599)
    // ========================================
    
    /// <summary>
    /// A projection model is required for an index with non-ALL projection (Strict mode).
    /// </summary>
    ProjectionModelRequired = 500
}
