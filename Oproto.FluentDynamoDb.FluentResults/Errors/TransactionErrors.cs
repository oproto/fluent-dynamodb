namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for transaction-related DynamoDB errors.
/// </summary>
public abstract class TransactionError : DynamoDbError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected TransactionError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected TransactionError(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a DynamoDB transaction was cancelled.
/// </summary>
public class TransactionCancelledError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "TRANSACTION_CANCELLED";

    /// <summary>
    /// Gets the reasons why the transaction was cancelled.
    /// </summary>
    public IReadOnlyList<string> CancellationReasons { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCancelledError"/> class.
    /// </summary>
    /// <param name="cancellationReasons">The reasons why the transaction was cancelled.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public TransactionCancelledError(IEnumerable<string> cancellationReasons, Exception? innerException = null)
        : base($"Transaction cancelled: {string.Join("; ", cancellationReasons)}", innerException)
    {
        CancellationReasons = cancellationReasons.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCancelledError"/> class with a single reason.
    /// </summary>
    /// <param name="reason">The reason why the transaction was cancelled.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public TransactionCancelledError(string reason, Exception? innerException = null)
        : this(new[] { reason }, innerException)
    {
    }
}

/// <summary>
/// Error indicating a transaction conflict occurred.
/// </summary>
public class TransactionConflictError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "TRANSACTION_CONFLICT";

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionConflictError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public TransactionConflictError(Exception? innerException = null)
        : base("Transaction conflict: Another transaction is in progress on one or more items", innerException)
    {
    }
}

/// <summary>
/// Error indicating a transaction is already in progress.
/// </summary>
public class TransactionInProgressError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "TRANSACTION_IN_PROGRESS";

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInProgressError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public TransactionInProgressError(Exception? innerException = null)
        : base("Transaction in progress: Cannot modify items that are part of another ongoing transaction", innerException)
    {
    }
}

/// <summary>
/// Error indicating provisioned throughput was exceeded.
/// </summary>
public class ProvisionedThroughputExceededError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "THROUGHPUT_EXCEEDED";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvisionedThroughputExceededError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ProvisionedThroughputExceededError(Exception? innerException = null)
        : base("Throughput exceeded: Transaction rate too high", innerException)
    {
    }
}

/// <summary>
/// Error indicating request limit was exceeded.
/// </summary>
public class RequestLimitExceededError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "REQUEST_LIMIT_EXCEEDED";

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimitExceededError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public RequestLimitExceededError(Exception? innerException = null)
        : base("Request limit exceeded: Too many concurrent transactions", innerException)
    {
    }
}

/// <summary>
/// Error indicating a DynamoDB resource was not found.
/// </summary>
public class ResourceNotFoundError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "RESOURCE_NOT_FOUND";

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ResourceNotFoundError(Exception? innerException = null)
        : base("Table or index not found", innerException)
    {
    }
}

/// <summary>
/// Error indicating an idempotency token mismatch.
/// </summary>
public class IdempotencyError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "IDEMPOTENCY_MISMATCH";

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public IdempotencyError(Exception? innerException = null)
        : base("Idempotency token mismatch: Duplicate transaction with different parameters", innerException)
    {
    }
}

/// <summary>
/// Error indicating a conditional check failed (optimistic locking).
/// </summary>
public class OptimisticLockingError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "OPTIMISTIC_LOCKING_FAILED";

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticLockingError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public OptimisticLockingError(string message = "Concurrent modification detected", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating item collection size limit was exceeded.
/// </summary>
public class CollectionSizeLimitError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "COLLECTION_SIZE_LIMIT";

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionSizeLimitError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public CollectionSizeLimitError(string message = "Item collection size limit exceeded", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a DynamoDB service error.
/// </summary>
public class ServiceError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "SERVICE_ERROR";

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ServiceError(string message = "DynamoDB service encountered an internal error", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a query iterator has expired.
/// </summary>
public class ExpiredIteratorError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "EXPIRED_ITERATOR";

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiredIteratorError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ExpiredIteratorError(string message = "Query iterator has expired", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a DynamoDB limit was exceeded.
/// </summary>
public class LimitExceededError : TransactionError
{
    /// <inheritdoc />
    public override string ErrorCode => "LIMIT_EXCEEDED";

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitExceededError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public LimitExceededError(string message = "DynamoDB limit exceeded for this operation", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
