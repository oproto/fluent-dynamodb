using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.FluentResults.UnitTests;

/// <summary>
/// Property-based tests for DynamoDbErrors.FromException method.
/// </summary>
public class DynamoDbErrorsPropertyTests
{
    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 1: Exception to Error Mapping Completeness**
    /// *For any* exception thrown by a DynamoDB operation, calling DynamoDbErrors.FromException SHALL return 
    /// a non-null DynamoDbError with a non-empty ErrorCode.
    /// **Validates: Requirements 1.2, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExceptionMapping_AlwaysReturnsNonNullErrorWithCode()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                var error = DynamoDbErrors.FromException(ex);
                return error != null && !string.IsNullOrEmpty(error.ErrorCode);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 5: Error Type Specificity**
    /// *For any* specific exception type (e.g., TransactionCanceledException, ConditionalCheckFailedException), 
    /// DynamoDbErrors.FromException SHALL return the corresponding specific error type.
    /// **Validates: Requirements 1.3, 14.1-14.17**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpecificExceptions_MapToSpecificErrorTypes()
    {
        return Prop.ForAll(
            SpecificExceptionArbitrary(),
            tuple =>
            {
                var (exception, expectedErrorType) = tuple;
                var error = DynamoDbErrors.FromException(exception);
                return expectedErrorType.IsInstanceOfType(error);
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 1: Exception to Error Mapping Completeness**
    /// *For any* exception, the resulting error should preserve the original exception.
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExceptionMapping_PreservesInnerException()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                var error = DynamoDbErrors.FromException(ex);
                return error.InnerException == ex;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 5: Error Type Specificity**
    /// *For any* AWS SDK DynamoDB exception, the error should be a DynamoDbError subclass.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AwsSdkExceptions_MapToDynamoDbErrors()
    {
        return Prop.ForAll(
            AwsSdkExceptionArbitrary(),
            ex =>
            {
                var error = DynamoDbErrors.FromException(ex);
                return error is DynamoDbError;
            });
    }

    /// <summary>
    /// Generates arbitrary exceptions for testing.
    /// </summary>
    private static Arbitrary<Exception> ExceptionArbitrary()
    {
        var generators = new[]
        {
            // AWS SDK exceptions
            Gen.Constant<Exception>(new TransactionCanceledException("Test")),
            Gen.Constant<Exception>(new TransactionConflictException("Test")),
            Gen.Constant<Exception>(new TransactionInProgressException("Test")),
            Gen.Constant<Exception>(new ProvisionedThroughputExceededException("Test")),
            Gen.Constant<Exception>(new RequestLimitExceededException("Test")),
            Gen.Constant<Exception>(new ResourceNotFoundException("Test")),
            Gen.Constant<Exception>(new IdempotentParameterMismatchException("Test")),
            Gen.Constant<Exception>(new ConditionalCheckFailedException("Test")),
            Gen.Constant<Exception>(new ItemCollectionSizeLimitExceededException("Test")),
            Gen.Constant<Exception>(new LimitExceededException("Test")),
            
            // Custom library exceptions
            Gen.Constant<Exception>(new DynamoDbMappingException("Test mapping error")),
            Gen.Constant<Exception>(new DiscriminatorMismatchException("Test discriminator error")),
            Gen.Constant<Exception>(new ProjectionValidationException("Test projection error")),
            Gen.Constant<Exception>(new ExpressionTranslationException("Test expression error")),
            Gen.Constant<Exception>(new BlobStorageException("Test blob error")),
            Gen.Constant<Exception>(new Oproto.FluentDynamoDb.Mapping.FieldEncryptionException("Test encryption error")),
            
            // InvalidOperationException variants
            Gen.Constant<Exception>(new InvalidOperationException("RequireWriteTransaction for entity 'TestEntity'")),
            Gen.Constant<Exception>(new InvalidOperationException("All operations must use the same DynamoDB client")),
            Gen.Constant<Exception>(new InvalidOperationException("Batch contains no operations")),
            Gen.Constant<Exception>(new InvalidOperationException("Batch exceeds maximum of 25 operations, 30 provided")),
            Gen.Constant<Exception>(new InvalidOperationException("No DynamoDB client configured")),
            Gen.Constant<Exception>(new InvalidOperationException("Cannot mix lambda and manual update expressions")),
            Gen.Constant<Exception>(new InvalidOperationException("Field encryption is required for 'Password'")),
            
            // ArgumentException variants
            Gen.Constant<Exception>(new ArgumentException("Collection cannot be empty", "items")),
            
            // FormatException
            Gen.Constant<Exception>(new FormatException("Invalid format string")),
            
            // Generic exceptions (fallback)
            Gen.Constant<Exception>(new Exception("Generic error")),
            Gen.Constant<Exception>(new InvalidOperationException("Some other error")),
        };

        return Arb.From(Gen.OneOf(generators));
    }

    /// <summary>
    /// Generates specific exception types with their expected error types.
    /// </summary>
    private static Arbitrary<(Exception, Type)> SpecificExceptionArbitrary()
    {
        var generators = new[]
        {
            // AWS SDK exceptions -> specific error types
            Gen.Constant<(Exception, Type)>((new TransactionCanceledException("Test"), typeof(TransactionCancelledError))),
            Gen.Constant<(Exception, Type)>((new TransactionConflictException("Test"), typeof(TransactionConflictError))),
            Gen.Constant<(Exception, Type)>((new TransactionInProgressException("Test"), typeof(TransactionInProgressError))),
            Gen.Constant<(Exception, Type)>((new ProvisionedThroughputExceededException("Test"), typeof(ProvisionedThroughputExceededError))),
            Gen.Constant<(Exception, Type)>((new RequestLimitExceededException("Test"), typeof(RequestLimitExceededError))),
            Gen.Constant<(Exception, Type)>((new ResourceNotFoundException("Test"), typeof(ResourceNotFoundError))),
            Gen.Constant<(Exception, Type)>((new IdempotentParameterMismatchException("Test"), typeof(IdempotencyError))),
            Gen.Constant<(Exception, Type)>((new ConditionalCheckFailedException("Test"), typeof(OptimisticLockingError))),
            Gen.Constant<(Exception, Type)>((new ItemCollectionSizeLimitExceededException("Test"), typeof(CollectionSizeLimitError))),
            Gen.Constant<(Exception, Type)>((new LimitExceededException("Test"), typeof(LimitExceededError))),
            
            // Custom library exceptions -> specific error types
            Gen.Constant<(Exception, Type)>((new DynamoDbMappingException("Test"), typeof(MappingError))),
            Gen.Constant<(Exception, Type)>((new DiscriminatorMismatchException("Test"), typeof(DiscriminatorMismatchError))),
            Gen.Constant<(Exception, Type)>((new ProjectionValidationException("Test"), typeof(ProjectionValidationError))),
            Gen.Constant<(Exception, Type)>((new ExpressionTranslationException("Test"), typeof(ExpressionTranslationError))),
            Gen.Constant<(Exception, Type)>((new BlobStorageException("Test"), typeof(BlobStorageError))),
            Gen.Constant<(Exception, Type)>((new Oproto.FluentDynamoDb.Mapping.FieldEncryptionException("Test"), typeof(EncryptionError))),
            
            // InvalidOperationException variants -> specific error types
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("RequireWriteTransaction"), typeof(WriteTransactionRequiredError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("same DynamoDB client"), typeof(ClientMismatchError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("no operations"), typeof(EmptyOperationError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("maximum of 25"), typeof(OperationLimitExceededError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("No DynamoDB client"), typeof(MissingClientError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("Cannot mix"), typeof(UpdateExpressionConflictError))),
            Gen.Constant<(Exception, Type)>((new InvalidOperationException("encryption is required"), typeof(EncryptionConfigurationError))),
            
            // ArgumentException -> EmptyCollectionError
            Gen.Constant<(Exception, Type)>((new ArgumentException("empty", "items"), typeof(EmptyCollectionError))),
            
            // FormatException -> FormatStringError
            Gen.Constant<(Exception, Type)>((new FormatException("Invalid format"), typeof(FormatStringError))),
        };

        return Arb.From(Gen.OneOf(generators));
    }

    /// <summary>
    /// Generates AWS SDK DynamoDB exceptions.
    /// </summary>
    private static Arbitrary<Exception> AwsSdkExceptionArbitrary()
    {
        var generators = new[]
        {
            Gen.Constant<Exception>(new TransactionCanceledException("Test")),
            Gen.Constant<Exception>(new TransactionConflictException("Test")),
            Gen.Constant<Exception>(new TransactionInProgressException("Test")),
            Gen.Constant<Exception>(new ProvisionedThroughputExceededException("Test")),
            Gen.Constant<Exception>(new RequestLimitExceededException("Test")),
            Gen.Constant<Exception>(new ResourceNotFoundException("Test")),
            Gen.Constant<Exception>(new IdempotentParameterMismatchException("Test")),
            Gen.Constant<Exception>(new ConditionalCheckFailedException("Test")),
            Gen.Constant<Exception>(new ItemCollectionSizeLimitExceededException("Test")),
            Gen.Constant<Exception>(new LimitExceededException("Test")),
        };

        return Arb.From(Gen.OneOf(generators));
    }
}
