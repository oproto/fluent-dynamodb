using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

/// <summary>
/// Tests verifying the public API surface of the encryption module.
/// Ensures interface contracts, class signatures, and property defaults are correct.
/// </summary>
public class PublicApiSurfaceTests
{
    #region IFieldEncryptor Interface

    [Fact]
    public void IFieldEncryptor_AwsEncryptionSdkFieldEncryptor_ImplementsInterface()
    {
        // Arrange
        var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        
        // Act
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        
        // Assert - Verify it implements IFieldEncryptor
        encryptor.Should().BeAssignableTo<IFieldEncryptor>();
    }

    [Fact]
    public void IFieldEncryptor_HasEncryptAsyncMethod()
    {
        // Verify the interface has the expected method signature
        var interfaceType = typeof(IFieldEncryptor);
        var method = interfaceType.GetMethod("EncryptAsync");
        
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<byte[]>));
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(4);
        parameters[0].ParameterType.Should().Be(typeof(byte[]));
        parameters[0].Name.Should().Be("plaintext");
        parameters[1].ParameterType.Should().Be(typeof(string));
        parameters[1].Name.Should().Be("fieldName");
        parameters[2].ParameterType.Should().Be(typeof(FieldEncryptionContext));
        parameters[2].Name.Should().Be("context");
        parameters[3].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[3].Name.Should().Be("cancellationToken");
    }

    [Fact]
    public void IFieldEncryptor_HasDecryptAsyncMethod()
    {
        // Verify the interface has the expected method signature
        var interfaceType = typeof(IFieldEncryptor);
        var method = interfaceType.GetMethod("DecryptAsync");
        
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<byte[]>));
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(4);
        parameters[0].ParameterType.Should().Be(typeof(byte[]));
        parameters[0].Name.Should().Be("ciphertext");
        parameters[1].ParameterType.Should().Be(typeof(string));
        parameters[1].Name.Should().Be("fieldName");
        parameters[2].ParameterType.Should().Be(typeof(FieldEncryptionContext));
        parameters[2].Name.Should().Be("context");
        parameters[3].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[3].Name.Should().Be("cancellationToken");
    }

    #endregion

    #region IKmsKeyResolver Interface

    [Fact]
    public void IKmsKeyResolver_DefaultKmsKeyResolver_ImplementsInterface()
    {
        // Act
        var resolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        
        // Assert
        resolver.Should().BeAssignableTo<IKmsKeyResolver>();
    }

    [Fact]
    public void IKmsKeyResolver_HasResolveKeyIdMethod()
    {
        // Verify the interface has the expected method signature
        var interfaceType = typeof(IKmsKeyResolver);
        var method = interfaceType.GetMethod("ResolveKeyId");
        
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(string));
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[0].Name.Should().Be("contextId");
    }

    [Fact]
    public void IKmsKeyResolver_CustomImplementation_CanBeUsed()
    {
        // Arrange - Create a custom implementation
        var customResolver = new TestKmsKeyResolver("custom-key-arn");
        
        // Act
        var keyArn = customResolver.ResolveKeyId("test-context");
        
        // Assert
        keyArn.Should().Be("custom-key-arn");
        customResolver.Should().BeAssignableTo<IKmsKeyResolver>();
    }

    #endregion

    #region AwsEncryptionSdkOptions Properties

    [Fact]
    public void AwsEncryptionSdkOptions_HasDefaultKeyIdProperty()
    {
        var options = new AwsEncryptionSdkOptions();
        
        // Verify property exists and has expected default
        options.DefaultKeyId.Should().Be(string.Empty);
        
        // Verify property can be set
        options.DefaultKeyId = "test-key";
        options.DefaultKeyId.Should().Be("test-key");
    }

    [Fact]
    public void AwsEncryptionSdkOptions_HasContextKeyMapProperty()
    {
        var options = new AwsEncryptionSdkOptions();
        
        // Verify property exists and has expected default
        options.ContextKeyMap.Should().BeNull();
        
        // Verify property can be set
        var map = new Dictionary<string, string> { ["tenant"] = "key" };
        options.ContextKeyMap = map;
        options.ContextKeyMap.Should().BeSameAs(map);
    }

    [Fact]
    public void AwsEncryptionSdkOptions_HasEnableCachingProperty()
    {
        var options = new AwsEncryptionSdkOptions();
        
        // Verify property exists and has expected default
        options.EnableCaching.Should().BeTrue();
        
        // Verify property can be set
        options.EnableCaching = false;
        options.EnableCaching.Should().BeFalse();
    }

    [Fact]
    public void AwsEncryptionSdkOptions_HasAlgorithmProperty()
    {
        var options = new AwsEncryptionSdkOptions();
        
        // Verify property exists and has expected default
        options.Algorithm.Should().Be("AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384");
        
        // Verify property can be set
        options.Algorithm = "AES_192_GCM_HKDF_SHA384_ECDSA_P384";
        options.Algorithm.Should().Be("AES_192_GCM_HKDF_SHA384_ECDSA_P384");
    }

    [Fact]
    public void AwsEncryptionSdkOptions_ObjectInitializerPattern_Works()
    {
        // This pattern should continue to work after migration
        var options = new AwsEncryptionSdkOptions
        {
            DefaultKeyId = "arn:aws:kms:us-east-1:123456789012:key/test-key",
            ContextKeyMap = new Dictionary<string, string>
            {
                ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key"
            },
            EnableCaching = true,
            Algorithm = "AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384"
        };
        
        options.DefaultKeyId.Should().NotBeEmpty();
        options.ContextKeyMap.Should().HaveCount(1);
        options.EnableCaching.Should().BeTrue();
    }

    #endregion

    #region FieldEncryptionContext Properties

    [Fact]
    public void FieldEncryptionContext_HasContextIdProperty()
    {
        var context = new FieldEncryptionContext();
        
        // Verify property exists and has expected default
        context.ContextId.Should().BeNull();
        
        // Verify property can be set via init
        var contextWithId = new FieldEncryptionContext { ContextId = "tenant-123" };
        contextWithId.ContextId.Should().Be("tenant-123");
    }

    [Fact]
    public void FieldEncryptionContext_HasCacheTtlSecondsProperty()
    {
        var context = new FieldEncryptionContext();
        
        // Verify property exists and has expected default (5 minutes)
        context.CacheTtlSeconds.Should().Be(300);
        
        // Verify property can be set via init
        var contextWithTtl = new FieldEncryptionContext { CacheTtlSeconds = 600 };
        contextWithTtl.CacheTtlSeconds.Should().Be(600);
    }

    [Fact]
    public void FieldEncryptionContext_HasIsExternalBlobProperty()
    {
        var context = new FieldEncryptionContext();
        
        // Verify property exists and has expected default
        context.IsExternalBlob.Should().BeFalse();
        
        // Verify property can be set via init
        var contextWithBlob = new FieldEncryptionContext { IsExternalBlob = true };
        contextWithBlob.IsExternalBlob.Should().BeTrue();
    }

    [Fact]
    public void FieldEncryptionContext_HasEntityIdProperty()
    {
        var context = new FieldEncryptionContext();
        
        // Verify property exists and has expected default
        context.EntityId.Should().BeNull();
        
        // Verify property can be set via init
        var contextWithEntityId = new FieldEncryptionContext { EntityId = "entity-123" };
        contextWithEntityId.EntityId.Should().Be("entity-123");
    }

    [Fact]
    public void FieldEncryptionContext_ObjectInitializerPattern_Works()
    {
        // This pattern should continue to work after migration
        var context = new FieldEncryptionContext
        {
            ContextId = "tenant-123",
            CacheTtlSeconds = 600,
            IsExternalBlob = false,
            EntityId = "entity-456"
        };
        
        context.ContextId.Should().Be("tenant-123");
        context.CacheTtlSeconds.Should().Be(600);
        context.IsExternalBlob.Should().BeFalse();
        context.EntityId.Should().Be("entity-456");
    }

    #endregion

    #region FieldEncryptionException

    [Fact]
    public void FieldEncryptionException_HasFieldNameProperty()
    {
        var exception = new FieldEncryptionException("Test message", "TestField");
        
        exception.FieldName.Should().Be("TestField");
    }

    [Fact]
    public void FieldEncryptionException_HasContextIdProperty()
    {
        var exception = new FieldEncryptionException("Test message", "TestField", "tenant-123", null);
        
        exception.ContextId.Should().Be("tenant-123");
    }

    [Fact]
    public void FieldEncryptionException_HasKeyIdProperty()
    {
        var exception = new FieldEncryptionException("Test message", "TestField", null, "key-arn");
        
        exception.KeyId.Should().Be("key-arn");
    }

    [Fact]
    public void FieldEncryptionException_Constructor_MessageAndFieldName_Works()
    {
        var exception = new FieldEncryptionException("Test message", "TestField");
        
        exception.Message.Should().Be("Test message");
        exception.FieldName.Should().Be("TestField");
        exception.ContextId.Should().BeNull();
        exception.KeyId.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void FieldEncryptionException_Constructor_WithContextAndKey_Works()
    {
        var exception = new FieldEncryptionException(
            "Test message", 
            "TestField", 
            "tenant-123", 
            "key-arn");
        
        exception.Message.Should().Be("Test message");
        exception.FieldName.Should().Be("TestField");
        exception.ContextId.Should().Be("tenant-123");
        exception.KeyId.Should().Be("key-arn");
    }

    [Fact]
    public void FieldEncryptionException_Constructor_WithInnerException_Works()
    {
        var innerException = new InvalidOperationException("Inner error");
        var exception = new FieldEncryptionException(
            "Test message", 
            "TestField", 
            innerException);
        
        exception.Message.Should().Be("Test message");
        exception.FieldName.Should().Be("TestField");
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void FieldEncryptionException_Constructor_FullParameters_Works()
    {
        var innerException = new InvalidOperationException("Inner error");
        var exception = new FieldEncryptionException(
            "Test message", 
            "TestField", 
            "tenant-123", 
            "key-arn",
            innerException);
        
        exception.Message.Should().Be("Test message");
        exception.FieldName.Should().Be("TestField");
        exception.ContextId.Should().Be("tenant-123");
        exception.KeyId.Should().Be("key-arn");
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void FieldEncryptionException_InheritsFromException()
    {
        var exception = new FieldEncryptionException("Test", "Field");
        
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void FieldEncryptionException_CanBeThrownAndCaught()
    {
        // Arrange
        FieldEncryptionException? caughtException = null;
        
        // Act
        try
        {
            throw new FieldEncryptionException("Test error", "TestField", "tenant-123", "key-arn");
        }
        catch (FieldEncryptionException ex)
        {
            caughtException = ex;
        }
        
        // Assert
        caughtException.Should().NotBeNull();
        caughtException!.FieldName.Should().Be("TestField");
        caughtException.ContextId.Should().Be("tenant-123");
        caughtException.KeyId.Should().Be("key-arn");
    }

    #endregion

    #region Constructor Patterns

    [Fact]
    public void AwsEncryptionSdkFieldEncryptor_CanBeCreatedWithKeyResolver()
    {
        // This is the standard creation pattern that should continue to work
        var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void AwsEncryptionSdkFieldEncryptor_CanBeCreatedWithKeyResolverAndOptions()
    {
        // This is the standard creation pattern with options
        var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        var options = new AwsEncryptionSdkOptions
        {
            EnableCaching = true,
            Algorithm = "AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384"
        };
        
        var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options);
        
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void AwsEncryptionSdkFieldEncryptor_CanBeUsedAsIFieldEncryptor()
    {
        // Verify the encryptor can be used through the interface
        var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        IFieldEncryptor encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
        
        encryptor.Should().NotBeNull();
    }

    [Fact]
    public void DefaultKmsKeyResolver_CanBeCreatedWithDefaultKey()
    {
        // Standard creation pattern
        var resolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/test-key");
        
        resolver.Should().NotBeNull();
        resolver.ResolveKeyId(null).Should().Be("arn:aws:kms:us-east-1:123456789012:key/test-key");
    }

    [Fact]
    public void DefaultKmsKeyResolver_CanBeCreatedWithContextKeyMap()
    {
        // Standard creation pattern with context map
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key"
        };
        
        var resolver = new DefaultKmsKeyResolver(
            "arn:aws:kms:us-east-1:123456789012:key/default-key",
            contextKeyMap);
        
        resolver.Should().NotBeNull();
        resolver.ResolveKeyId("tenant-a").Should().Be("arn:aws:kms:us-east-1:123456789012:key/tenant-a-key");
        resolver.ResolveKeyId("unknown").Should().Be("arn:aws:kms:us-east-1:123456789012:key/default-key");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// A simple test implementation of IKmsKeyResolver to verify custom implementations work.
    /// </summary>
    private class TestKmsKeyResolver : IKmsKeyResolver
    {
        private readonly string _keyArn;

        public TestKmsKeyResolver(string keyArn)
        {
            _keyArn = keyArn;
        }

        public string ResolveKeyId(string? contextId)
        {
            return _keyArn;
        }
    }

    #endregion
}
