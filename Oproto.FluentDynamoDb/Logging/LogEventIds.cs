namespace Oproto.FluentDynamoDb.Logging;

/// <summary>
/// Event IDs for DynamoDB operations.
/// Organized by category for easy filtering and monitoring.
/// </summary>
public static class LogEventIds
{
    // ========================================
    // Mapping Operations (1000-1999)
    // ========================================
    
    /// <summary>
    /// Event ID for when ToDynamoDb mapping starts.
    /// </summary>
    public const int MappingToDynamoDbStart = 1000;
    
    /// <summary>
    /// Event ID for when ToDynamoDb mapping completes successfully.
    /// </summary>
    public const int MappingToDynamoDbComplete = 1001;
    
    /// <summary>
    /// Event ID for when FromDynamoDb mapping starts.
    /// </summary>
    public const int MappingFromDynamoDbStart = 1010;
    
    /// <summary>
    /// Event ID for when FromDynamoDb mapping completes successfully.
    /// </summary>
    public const int MappingFromDynamoDbComplete = 1011;
    
    /// <summary>
    /// Event ID for when a property mapping operation starts.
    /// </summary>
    public const int MappingPropertyStart = 1020;
    
    /// <summary>
    /// Event ID for when a property mapping operation completes.
    /// </summary>
    public const int MappingPropertyComplete = 1021;
    
    /// <summary>
    /// Event ID for when a property is skipped during mapping (e.g., null or empty values).
    /// </summary>
    public const int MappingPropertySkipped = 1022;
    
    // ========================================
    // Type Conversions (2000-2999)
    // ========================================
    
    /// <summary>
    /// Event ID for when converting a Map (Dictionary) to/from DynamoDB format.
    /// </summary>
    public const int ConvertingMap = 2000;
    
    /// <summary>
    /// Event ID for when converting a Set (HashSet) to/from DynamoDB format.
    /// </summary>
    public const int ConvertingSet = 2010;
    
    /// <summary>
    /// Event ID for when converting a List to/from DynamoDB format.
    /// </summary>
    public const int ConvertingList = 2020;
    
    /// <summary>
    /// Event ID for when converting a Time-To-Live (TTL) value.
    /// </summary>
    public const int ConvertingTtl = 2030;
    
    /// <summary>
    /// Event ID for when serializing/deserializing a JSON blob.
    /// </summary>
    public const int ConvertingJsonBlob = 2040;
    
    /// <summary>
    /// Event ID for when processing a blob reference (e.g., S3 reference).
    /// </summary>
    public const int ConvertingBlobReference = 2050;
    
    // ========================================
    // Expression Translation (2500-2599)
    // ========================================
    
    /// <summary>
    /// Event ID for when translating LINQ expressions to DynamoDB syntax.
    /// </summary>
    public const int ExpressionTranslation = 2500;
    
    /// <summary>
    /// Event ID for when applying format strings during serialization.
    /// </summary>
    public const int ApplyingFormatString = 2510;
    
    /// <summary>
    /// Event ID for when parsing formatted values during deserialization.
    /// </summary>
    public const int ParsingFormattedValue = 2511;
    
    /// <summary>
    /// Event ID for when encrypting field values.
    /// </summary>
    public const int EncryptingField = 2520;
    
    /// <summary>
    /// Event ID for when decrypting field values.
    /// </summary>
    public const int DecryptingField = 2521;
    
    // ========================================
    // DynamoDB Operations (3000-3999)
    // ========================================
    
    /// <summary>
    /// Event ID for when executing a GetItem operation.
    /// </summary>
    public const int ExecutingGetItem = 3000;
    
    /// <summary>
    /// Event ID for when executing a PutItem operation.
    /// </summary>
    public const int ExecutingPutItem = 3010;
    
    /// <summary>
    /// Event ID for when executing a Query operation.
    /// </summary>
    public const int ExecutingQuery = 3020;
    
    /// <summary>
    /// Event ID for when executing an Update operation.
    /// </summary>
    public const int ExecutingUpdate = 3030;
    
    /// <summary>
    /// Event ID for when executing a Transaction operation.
    /// </summary>
    public const int ExecutingTransaction = 3040;
    
    /// <summary>
    /// Event ID for when executing a TransactionWrite operation.
    /// </summary>
    public const int ExecutingTransactionWrite = 3041;
    
    /// <summary>
    /// Event ID for when executing a TransactionGet operation.
    /// </summary>
    public const int ExecutingTransactionGet = 3042;
    
    /// <summary>
    /// Event ID for when executing a BatchWrite operation.
    /// </summary>
    public const int ExecutingBatchWrite = 3050;
    
    /// <summary>
    /// Event ID for when executing a BatchGet operation.
    /// </summary>
    public const int ExecutingBatchGet = 3051;
    
    /// <summary>
    /// Event ID for when a DynamoDB operation completes successfully.
    /// </summary>
    public const int OperationComplete = 3100;
    
    /// <summary>
    /// Event ID for logging consumed capacity information.
    /// </summary>
    public const int ConsumedCapacity = 3110;
    
    /// <summary>
    /// Event ID for logging unprocessed items in batch operations.
    /// </summary>
    public const int UnprocessedItems = 3120;
    
    /// <summary>
    /// Event ID for logging detailed operation breakdowns.
    /// </summary>
    public const int OperationBreakdown = 3130;
    
    // ========================================
    // Errors (9000-9999)
    // ========================================
    
    /// <summary>
    /// Event ID for errors during entity mapping operations.
    /// </summary>
    public const int MappingError = 9000;
    
    /// <summary>
    /// Event ID for errors during type conversion operations.
    /// </summary>
    public const int ConversionError = 9010;
    
    /// <summary>
    /// Event ID for errors during JSON serialization/deserialization.
    /// </summary>
    public const int JsonSerializationError = 9020;
    
    /// <summary>
    /// Event ID for errors during blob storage operations.
    /// </summary>
    public const int BlobStorageError = 9030;
    
    /// <summary>
    /// Event ID for errors during DynamoDB operations.
    /// </summary>
    public const int DynamoDbOperationError = 9040;
}
