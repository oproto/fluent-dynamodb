namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class FieldEncryptionExceptionTests
{
    private const string TestMessage = "Encryption failed";
    private const string TestFieldName = "SensitiveData";
    private const string TestContextId = "tenant-123";
    private const string TestKeyId = "arn:aws:kms:us-east-1:123456789012:key/test-key-id";

    [Fact]
    public void Constructor_WithMessageAndFieldName_SetsPropertiesCorrectly()
    {
        // Act
        var exception = new FieldEncryptionException(TestMessage, TestFieldName);

        // Assert
        exception.Message.Should().Be(TestMessage);
        exception.FieldName.Should().Be(TestFieldName);
        exception.ContextId.Should().BeNull();
        exception.KeyId.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Act
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            TestKeyId);

        // Assert
        exception.Message.Should().Be(TestMessage);
        exception.FieldName.Should().Be(TestFieldName);
        exception.ContextId.Should().Be(TestContextId);
        exception.KeyId.Should().Be(TestKeyId);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithInnerException_SetsPropertiesCorrectly()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            innerException);

        // Assert
        exception.Message.Should().Be(TestMessage);
        exception.FieldName.Should().Be(TestFieldName);
        exception.ContextId.Should().BeNull();
        exception.KeyId.Should().BeNull();
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_WithAllParametersAndInnerException_SetsPropertiesCorrectly()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            TestKeyId,
            innerException);

        // Assert
        exception.Message.Should().Be(TestMessage);
        exception.FieldName.Should().Be(TestFieldName);
        exception.ContextId.Should().Be(TestContextId);
        exception.KeyId.Should().Be(TestKeyId);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_WithNullFieldName_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FieldEncryptionException(TestMessage, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public void Constructor_WithNullFieldNameAndInnerException_ThrowsArgumentNullException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var act = () => new FieldEncryptionException(TestMessage, null!, innerException);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public void Constructor_WithNullContextId_AllowsNull()
    {
        // Act
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            contextId: null,
            keyId: TestKeyId);

        // Assert
        exception.ContextId.Should().BeNull();
        exception.FieldName.Should().Be(TestFieldName);
        exception.KeyId.Should().Be(TestKeyId);
    }

    [Fact]
    public void Constructor_WithNullKeyId_AllowsNull()
    {
        // Act
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            keyId: null);

        // Assert
        exception.KeyId.Should().BeNull();
        exception.FieldName.Should().Be(TestFieldName);
        exception.ContextId.Should().Be(TestContextId);
    }

    [Fact]
    public void ToString_WithMinimalProperties_IncludesFieldName()
    {
        // Arrange
        var exception = new FieldEncryptionException(TestMessage, TestFieldName);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(TestMessage);
        result.Should().Contain($"FieldName: {TestFieldName}");
        result.Should().NotContain("ContextId:");
        result.Should().NotContain("KeyId:");
    }

    [Fact]
    public void ToString_WithAllProperties_IncludesAllDetails()
    {
        // Arrange
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            TestKeyId);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(TestMessage);
        result.Should().Contain($"FieldName: {TestFieldName}");
        result.Should().Contain($"ContextId: {TestContextId}");
        result.Should().Contain($"KeyId: {TestKeyId}");
    }

    [Fact]
    public void ToString_WithContextIdOnly_IncludesContextId()
    {
        // Arrange
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            keyId: null);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain($"FieldName: {TestFieldName}");
        result.Should().Contain($"ContextId: {TestContextId}");
        result.Should().NotContain("KeyId:");
    }

    [Fact]
    public void ToString_WithKeyIdOnly_IncludesKeyId()
    {
        // Arrange
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            contextId: null,
            keyId: TestKeyId);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain($"FieldName: {TestFieldName}");
        result.Should().Contain($"KeyId: {TestKeyId}");
        result.Should().NotContain("ContextId:");
    }

    [Fact]
    public void ToString_WithInnerException_IncludesInnerExceptionDetails()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var exception = new FieldEncryptionException(
            TestMessage,
            TestFieldName,
            TestContextId,
            TestKeyId,
            innerException);

        // Act
        var result = exception.ToString();

        // Assert
        result.Should().Contain(TestMessage);
        result.Should().Contain("Inner error");
        result.Should().Contain($"FieldName: {TestFieldName}");
    }

    [Fact]
    public void CanBeThrown()
    {
        // Arrange
        var exception = new FieldEncryptionException(TestMessage, TestFieldName);

        // Act
        Action act = () => throw exception;

        // Assert
        act.Should().Throw<FieldEncryptionException>()
            .WithMessage(TestMessage)
            .Where(e => e.FieldName == TestFieldName);
    }

    [Fact]
    public void CanBeCaught()
    {
        // Arrange
        FieldEncryptionException? caughtException = null;

        // Act
        try
        {
            throw new FieldEncryptionException(TestMessage, TestFieldName, TestContextId, TestKeyId);
        }
        catch (FieldEncryptionException ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException!.FieldName.Should().Be(TestFieldName);
        caughtException.ContextId.Should().Be(TestContextId);
        caughtException.KeyId.Should().Be(TestKeyId);
    }
}
