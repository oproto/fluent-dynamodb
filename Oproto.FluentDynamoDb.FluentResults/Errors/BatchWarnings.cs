using FluentResults;

namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Warning indicating that a batch operation completed successfully but had unprocessed items.
/// These items should be retried with exponential backoff.
/// </summary>
public class UnprocessedItemsWarning : Success
{
    /// <summary>
    /// Gets the number of unprocessed items.
    /// </summary>
    public int UnprocessedCount { get; }

    /// <summary>
    /// Gets the table names that have unprocessed items.
    /// </summary>
    public IReadOnlyList<string> TableNames { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnprocessedItemsWarning"/> class.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="unprocessedCount">The number of unprocessed items.</param>
    /// <param name="tableNames">The table names with unprocessed items.</param>
    public UnprocessedItemsWarning(string message, int unprocessedCount, IReadOnlyList<string> tableNames)
        : base(message)
    {
        UnprocessedCount = unprocessedCount;
        TableNames = tableNames;
    }
}

/// <summary>
/// Warning indicating that a batch PartiQL statement had an error.
/// </summary>
public class BatchStatementErrorWarning : Success
{
    /// <summary>
    /// Gets the index of the statement that had an error.
    /// </summary>
    public int StatementIndex { get; }

    /// <summary>
    /// Gets the error code from DynamoDB.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the error message from DynamoDB.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchStatementErrorWarning"/> class.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="statementIndex">The index of the statement that had an error.</param>
    /// <param name="errorCode">The error code from DynamoDB.</param>
    /// <param name="errorMessage">The error message from DynamoDB.</param>
    public BatchStatementErrorWarning(string message, int statementIndex, string? errorCode, string? errorMessage)
        : base(message)
    {
        StatementIndex = statementIndex;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
