using System.Net;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Factory class for creating DynamoDB-specific FluentResults errors from exceptions.
/// </summary>
public static class DynamoDbErrors
{
    // Regex patterns for parsing InvalidOperationException messages
    private static readonly Regex OperationLimitRegex = new(@"maximum of (\d+).*?(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Converts any exception to an appropriate DynamoDbError.
    /// </summary>
    /// <param name="ex">The exception to convert.</param>
    /// <returns>A DynamoDbError representing the exception.</returns>
    public static DynamoDbError FromException(Exception ex)
    {
        return ex switch
        {
            // AWS SDK DynamoDB Exceptions
            TransactionCanceledException tce => CreateTransactionCancelledError(tce),
            TransactionConflictException tcex => new TransactionConflictError(tcex),
            TransactionInProgressException tipex => new TransactionInProgressError(tipex),
            ProvisionedThroughputExceededException ptex => new ProvisionedThroughputExceededError(ptex),
            RequestLimitExceededException rlex => new RequestLimitExceededError(rlex),
            ResourceNotFoundException rnfex => new ResourceNotFoundError(rnfex),
            IdempotentParameterMismatchException ipmex => new IdempotencyError(ipmex),
            ConditionalCheckFailedException ccfex => new OptimisticLockingError("Concurrent modification detected", ccfex),
            ItemCollectionSizeLimitExceededException icslex => new CollectionSizeLimitError("Item collection size limit exceeded", icslex),
            LimitExceededException lex => new LimitExceededError("DynamoDB limit exceeded for this operation", lex),
            AmazonDynamoDBException dbEx when dbEx.StatusCode == HttpStatusCode.InternalServerError
                => new ServiceError("DynamoDB service encountered an internal error", dbEx),
            AmazonDynamoDBException dbEx when dbEx.ErrorCode == "ExpiredIterator"
                => new ExpiredIteratorError("Query iterator has expired", dbEx),

            // Custom Library Exceptions - Mapping
            DynamoDbMappingException mex => new MappingError(
                mex.Message,
                mex.EntityType?.FullName,
                mex.PropertyName,
                mex),
            DiscriminatorMismatchException dmex => new DiscriminatorMismatchError(
                dmex.Message,
                dmex.ExpectedDiscriminator,
                dmex.ActualDiscriminator,
                dmex.ProjectionType?.FullName,
                dmex),
            ProjectionValidationException pvex => new ProjectionValidationError(
                pvex.Message,
                pvex.IndexName,
                pvex.ExpectedType?.FullName,
                pvex.ActualType?.FullName,
                pvex),

            // Custom Library Exceptions - Expression Translation
            ExpressionTranslationException etex => new ExpressionTranslationError(
                etex.Message,
                etex.OriginalExpression?.ToString(),
                etex),

            // Custom Library Exceptions - Validation
            SchemaValidationException svex => new SchemaValidationError(
                svex.ValidationResult.Errors.Select(e => e.Message),
                svex),

            // Custom Library Exceptions - Storage
            BlobStorageException bsex => new BlobStorageError(
                bsex.Message,
                bsex.ReferenceKey,
                operationType: null,
                bsex),

            // Custom Library Exceptions - Encryption (from Mapping namespace)
            Oproto.FluentDynamoDb.Mapping.FieldEncryptionException feex => new EncryptionError(
                feex.Message,
                fieldName: null,
                contextId: null,
                keyArn: null,
                feex),

            // InvalidOperationException with specific messages
            InvalidOperationException ioe when ioe.Message.Contains("RequireWriteTransaction", StringComparison.OrdinalIgnoreCase)
                => CreateWriteTransactionRequiredError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("same DynamoDB client", StringComparison.OrdinalIgnoreCase)
                => new ClientMismatchError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("no operations", StringComparison.OrdinalIgnoreCase)
                => new EmptyOperationError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("maximum of", StringComparison.OrdinalIgnoreCase)
                => CreateOperationLimitExceededError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("No DynamoDB client", StringComparison.OrdinalIgnoreCase)
                => new MissingClientError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("Cannot mix", StringComparison.OrdinalIgnoreCase)
                => new UpdateExpressionConflictError(ioe),
            InvalidOperationException ioe when ioe.Message.Contains("encryption is required", StringComparison.OrdinalIgnoreCase)
                => CreateEncryptionConfigurationError(ioe),

            // ArgumentException cases
            ArgumentException aex when aex.Message.Contains("empty", StringComparison.OrdinalIgnoreCase)
                => new EmptyCollectionError(aex.ParamName, aex),

            // FormatException cases
            FormatException fex => new FormatStringError(fex.Message, formatDetails: null, fex),

            // Fallback
            _ => new UnexpectedError(ex.Message, ex)
        };
    }

    private static TransactionCancelledError CreateTransactionCancelledError(TransactionCanceledException tce)
    {
        var reasons = tce.CancellationReasons?
            .Where(r => !string.IsNullOrEmpty(r.Message) || !string.IsNullOrEmpty(r.Code))
            .Select(r => !string.IsNullOrEmpty(r.Message) ? r.Message : r.Code ?? "Unknown")
            .ToList() ?? new List<string> { "Unknown reason" };

        if (reasons.Count == 0)
        {
            reasons.Add("Unknown reason");
        }

        return new TransactionCancelledError(reasons, tce);
    }

    private static WriteTransactionRequiredError CreateWriteTransactionRequiredError(InvalidOperationException ioe)
    {
        // Try to extract entity name from message
        var match = Regex.Match(ioe.Message, @"'([^']+)'");
        var entityName = match.Success ? match.Groups[1].Value : null;
        return new WriteTransactionRequiredError(entityName, ioe);
    }

    private static OperationLimitExceededError CreateOperationLimitExceededError(InvalidOperationException ioe)
    {
        var match = OperationLimitRegex.Match(ioe.Message);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var limit) && int.TryParse(match.Groups[2].Value, out var actual))
        {
            return new OperationLimitExceededError(limit, actual, ioe);
        }
        return new OperationLimitExceededError(0, 0, ioe);
    }

    private static EncryptionConfigurationError CreateEncryptionConfigurationError(InvalidOperationException ioe)
    {
        // Try to extract property names from message
        var matches = Regex.Matches(ioe.Message, @"'([^']+)'");
        var propertyNames = matches.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        return new EncryptionConfigurationError(ioe.Message, propertyNames, ioe);
    }
}