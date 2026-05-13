using Amazon.DynamoDBv2.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.UnitTests.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Providers.Encryption;

/// <summary>
/// Integration tests verifying decryption failure mode behavior for both read and write paths.
/// Requirements: 5.1, 5.2, 6.1, 6.2, 6.3
/// </summary>
public class DecryptionFailureModeTests
{
    #region 7.3 - Integrity failure always throws regardless of DecryptionFailureMode

    [Fact]
    public async Task FromDynamoDbAsync_SkipFieldsMode_InvalidCiphertext_ThrowsDynamoDbMappingException()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("invalid ciphertext: data is corrupted"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - integrity failures always throw, even in SkipFields mode
        Func<Task> act = async () => await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options);

        await act.Should().ThrowAsync<DynamoDbMappingException>();
    }

    [Fact]
    public async Task FromDynamoDbAsync_SkipFieldsMode_CannotDecrypt_ThrowsDynamoDbMappingException()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("cannot decrypt: key mismatch detected"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - integrity failures always throw, even in SkipFields mode
        Func<Task> act = async () => await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options);

        await act.Should().ThrowAsync<DynamoDbMappingException>();
    }

    [Fact]
    public async Task FromDynamoDbAsync_SkipFieldsMode_ContextValidationFailed_ThrowsDynamoDbMappingException()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("context validation failed: encryption context does not match"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - integrity failures always throw, even in SkipFields mode
        Func<Task> act = async () => await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options);

        await act.Should().ThrowAsync<DynamoDbMappingException>();
    }

    [Theory]
    [InlineData("invalid ciphertext: data is corrupted")]
    [InlineData("cannot decrypt: key mismatch detected")]
    [InlineData("context validation failed: encryption context does not match")]
    public async Task FromDynamoDbAsync_SkipFieldsMode_IntegrityFailure_WrapsOriginalException(string errorMessage)
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };

        var originalException = new FieldEncryptionException(errorMessage);
        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(originalException);

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - the DynamoDbMappingException wraps the original FieldEncryptionException
        Func<Task> act = async () => await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options);

        var exception = await act.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<FieldEncryptionException>();
    }

    #endregion

    #region 7.4 - Write behavior is unchanged regardless of DecryptionFailureMode

    [Fact]
    public async Task ToDynamoDbAsync_SkipFieldsMode_NullEncryptor_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var options = new FluentDynamoDbOptions()
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - writes always throw when encryptor is null, regardless of DecryptionFailureMode
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            fieldEncryptor: null,
            options: options);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ToDynamoDbAsync_ThrowMode_NullEncryptor_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var options = new FluentDynamoDbOptions()
            .WithDecryptionFailureMode(DecryptionFailureMode.Throw);

        // Act & Assert - writes always throw when encryptor is null
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            fieldEncryptor: null,
            options: options);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ToDynamoDbAsync_SkipFieldsMode_EncryptorThrows_ThrowsFieldEncryptionException()
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("KMS access denied for key arn:aws:kms:us-east-1:123:key/abc"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act & Assert - writes always throw when encryptor fails, regardless of DecryptionFailureMode
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            encryptor,
            options);

        await act.Should().ThrowAsync<FieldEncryptionException>();
    }

    [Fact]
    public async Task ToDynamoDbAsync_ThrowMode_EncryptorThrows_ThrowsFieldEncryptionException()
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("KMS access denied for key arn:aws:kms:us-east-1:123:key/abc"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.Throw);

        // Act & Assert - writes always throw when encryptor fails
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            encryptor,
            options);

        await act.Should().ThrowAsync<FieldEncryptionException>();
    }

    [Theory]
    [InlineData(DecryptionFailureMode.Throw)]
    [InlineData(DecryptionFailureMode.SkipFields)]
    public async Task ToDynamoDbAsync_DecryptionFailureMode_HasNoEffectOnWritePath_NullEncryptor(
        DecryptionFailureMode mode)
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var options = new FluentDynamoDbOptions()
            .WithDecryptionFailureMode(mode);

        // Act & Assert - both modes throw the same exception type for null encryptor
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            fieldEncryptor: null,
            options: options);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("[Encrypted]");
    }

    [Theory]
    [InlineData(DecryptionFailureMode.Throw)]
    [InlineData(DecryptionFailureMode.SkipFields)]
    public async Task ToDynamoDbAsync_DecryptionFailureMode_HasNoEffectOnWritePath_EncryptorThrows(
        DecryptionFailureMode mode)
    {
        // Arrange
        var entity = new EncryptionOnlyTestEntity
        {
            Pk = "test-pk",
            Name = "Test",
            SocialSecurityNumber = "123-45-6789"
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("Encryption failed: access denied"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(mode);

        // Act & Assert - both modes throw the same exception for encryptor failure
        Func<Task> act = async () => await EncryptionOnlyTestEntity.ToDynamoDbAsync<EncryptionOnlyTestEntity>(
            entity,
            null!,
            encryptor,
            options);

        await act.Should().ThrowAsync<FieldEncryptionException>();
    }

    #endregion

    #region 7.2 - SkipFields mode with access denied exceptions

    [Fact]
    public async Task FromDynamoDbAsync_SkipFieldsMode_AccessDenied_LeavesPropertyAtDefault_AndLogsWarning()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test User" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3, 4 }) }
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("KMS access denied for key arn:aws:kms:us-east-1:123:key/abc"));

        var logger = Substitute.For<IDynamoDbLogger>();
        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithLogger(logger)
            .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

        // Act
        var result = await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options,
            CancellationToken.None);

        // Assert - encrypted property stays at CLR default (null for string?)
        result.SocialSecurityNumber.Should().BeNull();
        result.Pk.Should().Be("test-pk");
        result.Name.Should().Be("Test User");

        // Assert - warning logged with field name and key ID
        logger.Received().LogWarning(
            LogEventIds.EncryptionFieldSkipped,
            Arg.Any<string>(),
            Arg.Is<object[]>(args =>
                args.Any(a => a.ToString()!.Contains("SocialSecurityNumber")) &&
                args.Any(a => a.ToString()!.Contains("access denied"))));
    }

    [Fact]
    public async Task FromDynamoDbAsync_ThrowMode_AccessDenied_ThrowsDynamoDbMappingException()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk" },
            ["name"] = new AttributeValue { S = "Test User" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3, 4 }) }
        };

        var encryptor = Substitute.For<IFieldEncryptor>();
        encryptor.DecryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new FieldEncryptionException("KMS access denied for key arn:aws:kms:us-east-1:123:key/abc"));

        var options = new FluentDynamoDbOptions()
            .WithEncryption(encryptor)
            .WithDecryptionFailureMode(DecryptionFailureMode.Throw);

        // Act & Assert - Throw mode wraps in DynamoDbMappingException
        Func<Task> act = async () => await EncryptionOnlyTestEntity.FromDynamoDbAsync<EncryptionOnlyTestEntity>(
            item,
            null,
            encryptor,
            options,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<FieldEncryptionException>();
        exception.Which.InnerException!.Message.Should().Contain("access denied");
    }

    #endregion
}
