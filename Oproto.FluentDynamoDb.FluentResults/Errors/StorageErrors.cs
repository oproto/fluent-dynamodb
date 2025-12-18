namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for storage-related DynamoDB errors.
/// </summary>
public abstract class StorageError : DynamoDbError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected StorageError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected StorageError(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a blob storage operation failure.
/// </summary>
public class BlobStorageError : StorageError
{
    /// <inheritdoc />
    public override string ErrorCode => "BLOB_STORAGE_ERROR";

    /// <summary>
    /// Gets the blob key involved in the failed operation.
    /// </summary>
    public string? BlobKey { get; }

    /// <summary>
    /// Gets the type of operation that failed.
    /// </summary>
    public string? OperationType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="blobKey">The blob key involved in the failed operation.</param>
    /// <param name="operationType">The type of operation that failed.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public BlobStorageError(string message, string? blobKey = null, string? operationType = null, Exception? innerException = null)
        : base(message, innerException)
    {
        BlobKey = blobKey;
        OperationType = operationType;
    }
}

/// <summary>
/// Error indicating an encryption operation failure.
/// </summary>
public class EncryptionError : StorageError
{
    /// <inheritdoc />
    public override string ErrorCode => "ENCRYPTION_FAILED";

    /// <summary>
    /// Gets the field name that failed to encrypt.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets the encryption context ID.
    /// </summary>
    public string? ContextId { get; }

    /// <summary>
    /// Gets the KMS key ARN used for encryption.
    /// </summary>
    public string? KeyArn { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="fieldName">The field name that failed to encrypt.</param>
    /// <param name="contextId">The encryption context ID.</param>
    /// <param name="keyArn">The KMS key ARN used for encryption.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public EncryptionError(string message, string? fieldName = null, string? contextId = null, string? keyArn = null, Exception? innerException = null)
        : base(message, innerException)
    {
        FieldName = fieldName;
        ContextId = contextId;
        KeyArn = keyArn;
    }
}

/// <summary>
/// Error indicating a decryption operation failure.
/// </summary>
public class DecryptionError : StorageError
{
    /// <inheritdoc />
    public override string ErrorCode => "DECRYPTION_FAILED";

    /// <summary>
    /// Gets the field name that failed to decrypt.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Gets the encryption context ID.
    /// </summary>
    public string? ContextId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecryptionError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="fieldName">The field name that failed to decrypt.</param>
    /// <param name="contextId">The encryption context ID.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public DecryptionError(string message, string? fieldName = null, string? contextId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        FieldName = fieldName;
        ContextId = contextId;
    }
}

/// <summary>
/// Error indicating a stream processing failure.
/// </summary>
public class StreamProcessingError : StorageError
{
    /// <inheritdoc />
    public override string ErrorCode => "STREAM_PROCESSING_ERROR";

    /// <summary>
    /// Gets the record sequence number that caused the failure.
    /// </summary>
    public string? SequenceNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamProcessingError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="sequenceNumber">The record sequence number that caused the failure.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public StreamProcessingError(string message, string? sequenceNumber = null, Exception? innerException = null)
        : base(message, innerException)
    {
        SequenceNumber = sequenceNumber;
    }
}

/// <summary>
/// Error indicating an unexpected error occurred.
/// </summary>
public class UnexpectedError : DynamoDbError
{
    /// <inheritdoc />
    public override string ErrorCode => "UNEXPECTED_ERROR";

    /// <summary>
    /// Initializes a new instance of the <see cref="UnexpectedError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public UnexpectedError(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
