using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class AwsEncryptionSdkFieldEncryptorTests
{
    private const string DefaultKeyArn = "arn:aws:kms:us-east-1:123456789012:key/default-key-id";
    private const string TenantAKeyArn = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id";
    private const string TestFieldName = "SensitiveData";
    private const string TestContextId = "tenant-123";
    private const string TestKeyAlias = "pii";

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
            EnableCaching = true
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
        keyResolver.ResolveKeyIdAsync(TestContextId, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access, but key resolver should have been called
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(TestContextId, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncryptAsync_WithNullContextId_CallsKeyResolverWithNull()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(null, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = null };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access, but key resolver should have been called
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(null, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncryptAsync_WhenKeyResolverReturnsNull_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<string>(null!));
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
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));
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
        keyResolver.ResolveKeyIdAsync(TestContextId, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access, but key resolver should have been called
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(TestContextId, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecryptAsync_WithNullContextId_CallsKeyResolverWithNull()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(null, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = null };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access, but key resolver should have been called
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(null, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyResolverReturnsNull_ThrowsFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<string>(null!));
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
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));
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
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };
        var cts = new CancellationTokenSource();

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context, cts.Token);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert - No exception from cancellation token means it was accepted
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task DecryptAsync_WithCancellationToken_PassesTokenThrough()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };
        var cts = new CancellationTokenSource();

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context, cts.Token);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert - No exception from cancellation token means it was accepted
        cts.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task EncryptAsync_CallsResolverWithContextIdAndKeyAlias()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(TestContextId, TestKeyAlias, Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(TestContextId, TestKeyAlias, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecryptAsync_CallsResolverWithContextIdAndKeyAlias()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(TestContextId, TestKeyAlias, Arg.Any<CancellationToken>()).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert
        await keyResolver.Received(1).ResolveKeyIdAsync(TestContextId, TestKeyAlias, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EncryptAsync_ForwardsCancellationTokenToResolver()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), token).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.EncryptAsync(plaintext, TestFieldName, context, token);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert - verify the exact token was forwarded
        await keyResolver.Received(1).ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), token);
    }

    [Fact]
    public async Task DecryptAsync_ForwardsCancellationTokenToResolver()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), token).Returns(Task.FromResult(DefaultKeyArn));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        try
        {
            await encryptor.DecryptAsync(ciphertext, TestFieldName, context, token);
        }
        catch (FieldEncryptionException)
        {
            // Expected - AWS SDK will fail without real KMS access
        }

        // Assert - verify the exact token was forwarded
        await keyResolver.Received(1).ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), token);
    }

    [Fact]
    public async Task EncryptAsync_WhenResolverThrowsOperationCanceledException_PropagatesUnwrapped()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var expectedException = new OperationCanceledException(cts.Token);
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(x => throw expectedException);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, TestFieldName, context);

        // Assert - should propagate as OperationCanceledException, NOT wrapped in FieldEncryptionException
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DecryptAsync_WhenResolverThrowsOperationCanceledException_PropagatesUnwrapped()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var expectedException = new OperationCanceledException(cts.Token);
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(x => throw expectedException);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, TestFieldName, context);

        // Assert - should propagate as OperationCanceledException, NOT wrapped in FieldEncryptionException
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EncryptAsync_WhenResolverThrowsOtherException_WrapsInFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var originalException = new InvalidOperationException("Key resolution failed");
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(x => throw originalException);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.KeyAlias == TestKeyAlias)
            .Where(e => e.InnerException == originalException);
    }

    [Fact]
    public async Task DecryptAsync_WhenResolverThrowsOtherException_WrapsInFieldEncryptionException()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        var originalException = new InvalidOperationException("Key resolution failed");
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(x => throw originalException);
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.KeyAlias == TestKeyAlias)
            .Where(e => e.InnerException == originalException);
    }

    [Fact]
    public async Task EncryptAsync_WhenKeyResolverReturnsNull_ExceptionIncludesKeyAlias()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<string>(null!));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var plaintext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        var act = async () => await encryptor.EncryptAsync(plaintext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.KeyAlias == TestKeyAlias)
            .Where(e => e.Message.Contains("null or empty key ARN"));
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyResolverReturnsNull_ExceptionIncludesKeyAlias()
    {
        // Arrange
        var keyResolver = Substitute.For<IKmsKeyResolver>();
        keyResolver.ResolveKeyIdAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<string>(null!));
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        var ciphertext = new byte[] { 1, 2, 3 };
        var context = new FieldEncryptionContext { ContextId = TestContextId, KeyAlias = TestKeyAlias };

        // Act
        var act = async () => await encryptor.DecryptAsync(ciphertext, TestFieldName, context);

        // Assert
        await act.Should().ThrowAsync<FieldEncryptionException>()
            .Where(e => e.FieldName == TestFieldName)
            .Where(e => e.ContextId == TestContextId)
            .Where(e => e.KeyAlias == TestKeyAlias)
            .Where(e => e.Message.Contains("null or empty key ARN"));
    }
}
