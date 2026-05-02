namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

public class AwsEncryptionSdkOptionsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var options = new AwsEncryptionSdkOptions();

        // Assert
        options.DefaultKeyId.Should().Be(string.Empty);
        options.ContextKeyMap.Should().BeNull();
        options.EnableCaching.Should().BeTrue();
        options.Algorithm.Should().Be("AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384");
    }

    [Fact]
    public void DefaultKeyId_CanBeSet()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions();
        var keyArn = "arn:aws:kms:us-east-1:123456789012:key/test-key-id";

        // Act
        options.DefaultKeyId = keyArn;

        // Assert
        options.DefaultKeyId.Should().Be(keyArn);
    }

    [Fact]
    public void ContextKeyMap_CanBeSet()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions();
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key",
            ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key"
        };

        // Act
        options.ContextKeyMap = contextKeyMap;

        // Assert
        options.ContextKeyMap.Should().BeSameAs(contextKeyMap);
        options.ContextKeyMap.Should().HaveCount(2);
    }

    [Fact]
    public void EnableCaching_CanBeSetToFalse()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions();

        // Act
        options.EnableCaching = false;

        // Assert
        options.EnableCaching.Should().BeFalse();
    }

    [Fact]
    public void Algorithm_CanBeCustomized()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions();
        var customAlgorithm = "AES_192_GCM_HKDF_SHA384_ECDSA_P384";

        // Act
        options.Algorithm = customAlgorithm;

        // Assert
        options.Algorithm.Should().Be(customAlgorithm);
    }

    [Fact]
    public void AllProperties_CanBeSetViaObjectInitializer()
    {
        // Arrange
        var contextKeyMap = new Dictionary<string, string>
        {
            ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key"
        };

        // Act
        var options = new AwsEncryptionSdkOptions
        {
            DefaultKeyId = "arn:aws:kms:us-east-1:123456789012:key/default-key",
            ContextKeyMap = contextKeyMap,
            EnableCaching = false,
            Algorithm = "AES_192_GCM_HKDF_SHA384_ECDSA_P384"
        };

        // Assert
        options.DefaultKeyId.Should().Be("arn:aws:kms:us-east-1:123456789012:key/default-key");
        options.ContextKeyMap.Should().BeSameAs(contextKeyMap);
        options.EnableCaching.Should().BeFalse();
        options.Algorithm.Should().Be("AES_192_GCM_HKDF_SHA384_ECDSA_P384");
    }

    [Fact]
    public void Algorithm_DefaultValue_UsesKeyCommitment()
    {
        // Arrange & Act
        var options = new AwsEncryptionSdkOptions();

        // Assert
        options.Algorithm.Should().Contain("COMMIT_KEY");
        options.Algorithm.Should().Be("AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384");
    }

    [Fact]
    public void EnableCaching_DefaultValue_IsTrue()
    {
        // Arrange & Act
        var options = new AwsEncryptionSdkOptions();

        // Assert
        options.EnableCaching.Should().BeTrue();
    }

    [Fact]
    public void ContextKeyMap_CanBeSetToNull()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions
        {
            ContextKeyMap = new Dictionary<string, string>()
        };

        // Act
        options.ContextKeyMap = null;

        // Assert
        options.ContextKeyMap.Should().BeNull();
    }

    [Fact]
    public void DefaultKeyId_CanBeSetToEmptyString()
    {
        // Arrange
        var options = new AwsEncryptionSdkOptions
        {
            DefaultKeyId = "some-key"
        };

        // Act
        options.DefaultKeyId = string.Empty;

        // Assert
        options.DefaultKeyId.Should().Be(string.Empty);
    }

    [Fact]
    public void MultipleInstances_AreIndependent()
    {
        // Arrange & Act
        var options1 = new AwsEncryptionSdkOptions
        {
            DefaultKeyId = "key-1",
            EnableCaching = false
        };

        var options2 = new AwsEncryptionSdkOptions
        {
            DefaultKeyId = "key-2",
            EnableCaching = true
        };

        // Assert
        options1.DefaultKeyId.Should().Be("key-1");
        options1.EnableCaching.Should().BeFalse();
        options2.DefaultKeyId.Should().Be("key-2");
        options2.EnableCaching.Should().BeTrue();
    }
}
