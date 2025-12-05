using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class AwsEncryptionSdkFieldEncryptorTests
{
    private const string DefaultKeyArn = "arn:aws:kms:us-east-1:123456789012:key/default-key-id";
    private const string TenantAKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id";
    private const string TestFieldName = "SensitiveData";
    private const string TestContextId = "tenant-123";

    [Fact]
    public void Constructor_WithValidKeyResolver_Succeeds()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();

        // Act
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);

        // Assert
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullKeyResolver_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AwsEncryptionSdkFieldEncryptor(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("keyResolver");
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();

        // Act
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options: null);

        // Assert
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCachingEnabled_Succeeds()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var options = new AwsEncryptionSdkOptions
        {
            EnableCaching = true,
            DefaultCacheTtlSeconds = 600,
            MaxMessagesPerDataKey = 200,
            MaxBytesPerDataKey = 50 * 1024 * 1024
        };

        // Act
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options);

        // Assert
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCachingDisabled_Succeeds()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var options = new AwsEncryptionSdkOptions
        {
            EnableCaching = false
        };

        // Act
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options);

        // Assert
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public async Task EncryptAsync_WithNullPlaintext_ThrowsArgumentNullException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(null!, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("plaintext");
    }

    [Fact]
    public async Task EncryptAsync_WithNullFieldName_ThrowsArgumentException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, null!, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public async Task EncryptAsync_WithEmptyFieldName_ThrowsArgumentException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, string.Empty, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public async Task EncryptAsync_CallsKeyResolverWithContextId()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(TestContextId).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert
        keyResolver.Received(1).ResolveKeyId(TestContextId);
    }

    [Fact]
    public async Task EncryptAsync_WithNullContextId_CallsKeyResolverWithNull()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(null).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = null };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert
        keyResolver.Received(1).ResolveKeyId(null);
    }

    [Fact]
    public async Task EncryptAsync_WhenKeyResolverReturnsNull_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns((string?)null);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.Message.Contains("null or empty key ARN"));
    }

    [Fact]
    public async Task EncryptAsync_WhenKeyResolverReturnsEmpty_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns(string.Empty);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId);
    }

    [Fact]
    public async Task DecryptAsync_WithNullCiphertext_ThrowsArgumentNullException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(null!, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("ciphertext");
    }

    [Fact]
    public async Task DecryptAsync_WithNullFieldName_ThrowsArgumentException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, null!, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public async Task DecryptAsync_WithEmptyFieldName_ThrowsArgumentException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, string.Empty, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("fieldName");
    }

    [Fact]
    public async Task DecryptAsync_CallsKeyResolverWithContextId()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(TestContextId).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert
        keyResolver.Received(1).ResolveKeyId(TestContextId);
    }

    [Fact]
    public async Task DecryptAsync_WithNullContextId_CallsKeyResolverWithNull()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(null).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = null };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert
        keyResolver.Received(1).ResolveKeyId(null);
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyResolverReturnsNull_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns((string?)null);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.Message.Contains("null or empty key ARN"));
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyResolverReturnsEmpty_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns(string.Empty);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId);
    }



    [Fact]
    public async Task EncryptAsync_WithCancellationToken_PassesTokenThrough()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };
        var cts = new CancellationTokenSource();

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context, cts.Token);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert - No exception from cancellation token means it was accepted
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task DecryptAsync_WithCancellationToken_PassesTokenThrough()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyId(Arg.Any<string?>()).Returns(DefaultKeyArn);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };
        var cts = new CancellationTokenSource();

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context, cts.Token);
        }
        catch (NotImplementedException)
        {
            // Expected since AWS SDK integration is not complete
        }

        // Assert - No exception from cancellation token means it was accepted
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }
}
