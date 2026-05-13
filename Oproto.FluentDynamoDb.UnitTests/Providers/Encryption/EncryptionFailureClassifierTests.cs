using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.UnitTests.Providers.Encryption;

public class EncryptionFailureClassifierTests
{
    [Fact]
    public void IsIntegrityFailure_ReturnsTrueForInvalidCiphertext()
    {
        var ex = new Exception("The operation failed: invalid ciphertext received");

        EncryptionFailureClassifier.IsIntegrityFailure(ex).Should().BeTrue();
    }

    [Fact]
    public void IsIntegrityFailure_ReturnsTrueForCannotDecrypt()
    {
        var ex = new Exception("cannot decrypt the data key");

        EncryptionFailureClassifier.IsIntegrityFailure(ex).Should().BeTrue();
    }

    [Fact]
    public void IsIntegrityFailure_ReturnsTrueForContextValidationFailed()
    {
        var ex = new Exception("encryption context validation failed for field");

        EncryptionFailureClassifier.IsIntegrityFailure(ex).Should().BeTrue();
    }

    [Fact]
    public void IsIntegrityFailure_ReturnsFalseForAccessDenied()
    {
        var ex = new Exception("User: arn:aws:iam::123456:role/test is not authorized - access denied");

        EncryptionFailureClassifier.IsIntegrityFailure(ex).Should().BeFalse();
    }

    [Fact]
    public void IsRecoverable_ReturnsTrueForAccessDenied()
    {
        var ex = new Exception("KMS access denied for key arn:aws:kms:us-east-1:123456:key/abc");

        EncryptionFailureClassifier.IsRecoverable(ex).Should().BeTrue();
    }

    [Fact]
    public void IsRecoverable_ReturnsFalseForIntegrityFailure()
    {
        var ex = new Exception("invalid ciphertext: MAC check failed");

        EncryptionFailureClassifier.IsRecoverable(ex).Should().BeFalse();
    }

    [Theory]
    [InlineData("INVALID CIPHERTEXT")]
    [InlineData("Invalid Ciphertext")]
    [InlineData("CANNOT DECRYPT")]
    [InlineData("Cannot Decrypt")]
    [InlineData("CONTEXT VALIDATION FAILED")]
    [InlineData("Context Validation Failed")]
    public void IsIntegrityFailure_IsCaseInsensitive(string message)
    {
        var ex = new Exception(message);

        EncryptionFailureClassifier.IsIntegrityFailure(ex).Should().BeTrue();
    }

    [Fact]
    public void IsIntegrityFailure_ChecksInnerExceptionMessage()
    {
        var inner = new InvalidOperationException("invalid ciphertext detected");
        var outer = new Exception("A decryption error occurred", inner);

        EncryptionFailureClassifier.IsIntegrityFailure(outer).Should().BeTrue();
    }

    [Fact]
    public void IsIntegrityFailure_ReturnsFalseWhenNeitherMessageContainsIntegrityKeywords()
    {
        var inner = new InvalidOperationException("connection timeout");
        var outer = new Exception("operation failed", inner);

        EncryptionFailureClassifier.IsIntegrityFailure(outer).Should().BeFalse();
    }

    [Fact]
    public void IsRecoverable_ReturnsTrueWhenInnerExceptionHasAccessDenied()
    {
        var inner = new UnauthorizedAccessException("access denied to KMS key");
        var outer = new Exception("decryption failed", inner);

        EncryptionFailureClassifier.IsRecoverable(outer).Should().BeTrue();
    }
}
