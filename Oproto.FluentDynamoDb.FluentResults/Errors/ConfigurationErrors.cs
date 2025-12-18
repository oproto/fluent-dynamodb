namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for configuration-related DynamoDB errors.
/// </summary>
public abstract class ConfigurationError : DynamoDbError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected ConfigurationError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected ConfigurationError(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a DynamoDB client is missing.
/// </summary>
public class MissingClientError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "MISSING_CLIENT";

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingClientError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public MissingClientError(Exception? innerException = null)
        : base("No DynamoDB client configured", innerException)
    {
    }
}

/// <summary>
/// Error indicating encryption configuration is missing or invalid.
/// </summary>
public class EncryptionConfigurationError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "ENCRYPTION_CONFIGURATION_ERROR";

    /// <summary>
    /// Gets the property names that require encryption configuration.
    /// </summary>
    public IReadOnlyList<string> PropertyNames { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionConfigurationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="propertyNames">The property names that require encryption configuration.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public EncryptionConfigurationError(string message, IEnumerable<string>? propertyNames = null, Exception? innerException = null)
        : base(message, innerException)
    {
        PropertyNames = (propertyNames?.ToList() ?? new List<string>()).AsReadOnly();
    }
}

/// <summary>
/// Error indicating a write transaction is required for the operation.
/// </summary>
public class WriteTransactionRequiredError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "WRITE_TRANSACTION_REQUIRED";

    /// <summary>
    /// Gets the entity name that requires a write transaction.
    /// </summary>
    public string? EntityName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteTransactionRequiredError"/> class.
    /// </summary>
    /// <param name="entityName">The entity name that requires a write transaction.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public WriteTransactionRequiredError(string? entityName = null, Exception? innerException = null)
        : base(entityName != null
            ? $"Entity '{entityName}' requires a write transaction"
            : "Operation requires a write transaction", innerException)
    {
        EntityName = entityName;
    }
}

/// <summary>
/// Error indicating a client mismatch in batch or transaction operations.
/// </summary>
public class ClientMismatchError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "CLIENT_MISMATCH";

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientMismatchError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ClientMismatchError(Exception? innerException = null)
        : base("All operations in a batch or transaction must use the same DynamoDB client", innerException)
    {
    }
}

/// <summary>
/// Error indicating an empty batch or transaction operation.
/// </summary>
public class EmptyOperationError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "EMPTY_OPERATION";

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyOperationError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public EmptyOperationError(Exception? innerException = null)
        : base("Batch or transaction contains no operations", innerException)
    {
    }
}

/// <summary>
/// Error indicating an operation limit was exceeded.
/// </summary>
public class OperationLimitExceededError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "OPERATION_LIMIT_EXCEEDED";

    /// <summary>
    /// Gets the maximum allowed number of operations.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the actual number of operations attempted.
    /// </summary>
    public int ActualCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLimitExceededError"/> class.
    /// </summary>
    /// <param name="limit">The maximum allowed number of operations.</param>
    /// <param name="actualCount">The actual number of operations attempted.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public OperationLimitExceededError(int limit, int actualCount, Exception? innerException = null)
        : base($"Operation limit exceeded: maximum of {limit} operations allowed, but {actualCount} were provided", innerException)
    {
        Limit = limit;
        ActualCount = actualCount;
    }
}

/// <summary>
/// Error indicating conflicting update expression approaches.
/// </summary>
public class UpdateExpressionConflictError : ConfigurationError
{
    /// <inheritdoc />
    public override string ErrorCode => "UPDATE_EXPRESSION_CONFLICT";

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExpressionConflictError"/> class.
    /// </summary>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public UpdateExpressionConflictError(Exception? innerException = null)
        : base("Cannot mix different update expression approaches (lambda, format string, manual)", innerException)
    {
    }
}
