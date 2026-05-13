using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.UnitTests.Providers.Encryption;

/// <summary>
/// Property-based tests for EncryptionFailureClassifier.
/// Validates: Requirements 7.1, 7.2, 7.4
/// </summary>
public class EncryptionFailureClassifierPropertyTests
{
    /// <summary>
    /// Property 1: IsRecoverable is the logical complement of IsIntegrityFailure.
    /// For any exception, IsRecoverable(ex) == !IsIntegrityFailure(ex).
    /// **Validates: Requirements 7.1, 7.2, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsRecoverable_IsLogicalComplement_OfIsIntegrityFailure()
    {
        return Prop.ForAll(
            ExceptionArbitrary(),
            ex =>
            {
                var isRecoverable = EncryptionFailureClassifier.IsRecoverable(ex);
                var isIntegrityFailure = EncryptionFailureClassifier.IsIntegrityFailure(ex);

                return (isRecoverable == !isIntegrityFailure).ToProperty()
                    .Label($"IsRecoverable({isRecoverable}) should equal !IsIntegrityFailure({isIntegrityFailure}) " +
                           $"for message: \"{ex.Message}\"");
            });
    }

    /// <summary>
    /// Generates arbitrary exceptions with messages that include both integrity keywords
    /// and non-integrity messages to ensure broad coverage.
    /// </summary>
    private static Arbitrary<Exception> ExceptionArbitrary()
    {
        var integrityKeywords = new[]
        {
            "invalid ciphertext",
            "cannot decrypt",
            "context validation failed"
        };

        var nonIntegrityMessages = new[]
        {
            "access denied",
            "kms key not found",
            "timeout expired",
            "network error",
            "throttling exception",
            "service unavailable",
            ""
        };

        var messageGen = Gen.OneOf(
            // Generate messages with integrity keywords
            Gen.Elements(integrityKeywords),
            // Generate non-integrity messages
            Gen.Elements(nonIntegrityMessages),
            // Generate random strings that won't contain integrity keywords
            Arb.Default.NonEmptyString().Generator.Select(s => s.Get),
            // Generate messages with integrity keywords embedded in longer text
            Gen.Elements(integrityKeywords).Select(k => $"Error occurred: {k} during operation"),
            // Generate messages with mixed case integrity keywords
            Gen.Elements(integrityKeywords).Select(k => k.ToUpperInvariant())
        );

        var innerExceptionGen = Gen.OneOf(
            Gen.Constant<Exception?>(null),
            messageGen.Select(msg => (Exception?)new Exception(msg))
        );

        var exceptionGen = Gen.Zip(messageGen, innerExceptionGen)
            .Select(tuple =>
            {
                var (message, inner) = tuple;
                return inner != null
                    ? new Exception(message, inner)
                    : new Exception(message);
            });

        return Arb.From(exceptionGen);
    }
}
