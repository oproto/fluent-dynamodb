using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.UnitTests;

/// <summary>
/// Unit tests for KeyInputMode enum and FluentDynamoDbOptions integration.
/// Validates: Requirements 1.1, 2.1, 2.2, 2.3, 2.4, 2.5, 6.5
/// </summary>
public class KeyInputModeTests
{
    #region Enum Ordinal Values (Requirement 1.1)

    [Fact]
    public void KeyInputMode_Default_HasOrdinalZero()
    {
        ((int)KeyInputMode.Default).Should().Be(0);
    }

    [Fact]
    public void KeyInputMode_Auto_HasOrdinalOne()
    {
        ((int)KeyInputMode.Auto).Should().Be(1);
    }

    [Fact]
    public void KeyInputMode_Value_HasOrdinalTwo()
    {
        ((int)KeyInputMode.Value).Should().Be(2);
    }

    [Fact]
    public void KeyInputMode_Raw_HasOrdinalThree()
    {
        ((int)KeyInputMode.Raw).Should().Be(3);
    }

    #endregion

    #region Default Value (Requirement 2.1)

    [Fact]
    public void FluentDynamoDbOptions_DefaultKeyInputMode_IsAuto()
    {
        var options = new FluentDynamoDbOptions();

        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Auto);
    }

    #endregion

    #region UseKeyInputMode Throws for Default (Requirement 2.2)

    [Fact]
    public void UseKeyInputMode_WithDefault_ThrowsArgumentException()
    {
        var options = new FluentDynamoDbOptions();

        var act = () => options.UseKeyInputMode(KeyInputMode.Default);

        act.Should().Throw<ArgumentException>()
            .WithMessage("KeyInputMode.Default is only valid as a per-call parameter value. " +
                         "Specify Auto, Value, or Raw for the global default.*");
    }

    #endregion

    #region UseKeyInputMode Returns New Instance / Immutability (Requirements 2.3, 2.4)

    [Fact]
    public void UseKeyInputMode_ReturnsNewInstance()
    {
        var original = new FluentDynamoDbOptions();

        var result = original.UseKeyInputMode(KeyInputMode.Raw);

        result.Should().NotBeSameAs(original);
    }

    [Fact]
    public void UseKeyInputMode_OriginalInstanceUnchanged()
    {
        var original = new FluentDynamoDbOptions();

        original.UseKeyInputMode(KeyInputMode.Raw);

        original.DefaultKeyInputMode.Should().Be(KeyInputMode.Auto);
    }

    [Theory]
    [InlineData(KeyInputMode.Auto)]
    [InlineData(KeyInputMode.Value)]
    [InlineData(KeyInputMode.Raw)]
    public void UseKeyInputMode_SetsCorrectMode(KeyInputMode mode)
    {
        var options = new FluentDynamoDbOptions();

        var result = options.UseKeyInputMode(mode);

        result.DefaultKeyInputMode.Should().Be(mode);
    }

    #endregion

    #region Properties Preserved After UseKeyInputMode (Requirement 2.5)

    [Fact]
    public void UseKeyInputMode_PreservesLogger()
    {
        var logger = Substitute.For<IDynamoDbLogger>();
        var options = new FluentDynamoDbOptions().WithLogger(logger);

        var result = options.UseKeyInputMode(KeyInputMode.Value);

        result.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void UseKeyInputMode_PreservesBlobStorageProvider()
    {
        var provider = Substitute.For<IBlobStorageProvider>();
        var options = new FluentDynamoDbOptions().WithBlobStorage(provider);

        var result = options.UseKeyInputMode(KeyInputMode.Value);

        result.BlobStorageProvider.Should().BeSameAs(provider);
    }

    [Fact]
    public void UseKeyInputMode_PreservesFieldEncryptor()
    {
        var encryptor = Substitute.For<IFieldEncryptor>();
        var options = new FluentDynamoDbOptions().WithEncryption(encryptor);

        var result = options.UseKeyInputMode(KeyInputMode.Raw);

        result.FieldEncryptor.Should().BeSameAs(encryptor);
    }

    [Fact]
    public void UseKeyInputMode_PreservesConsistentRead()
    {
        var options = new FluentDynamoDbOptions().UseConsistentRead(true);

        var result = options.UseKeyInputMode(KeyInputMode.Raw);

        result.DefaultConsistentRead.Should().Be(true);
    }

    [Fact]
    public void UseKeyInputMode_PreservesAllConfiguredProperties()
    {
        var logger = Substitute.For<IDynamoDbLogger>();
        var blobProvider = Substitute.For<IBlobStorageProvider>();
        var encryptor = Substitute.For<IFieldEncryptor>();

        var options = new FluentDynamoDbOptions()
            .WithLogger(logger)
            .WithBlobStorage(blobProvider)
            .WithEncryption(encryptor)
            .UseConsistentRead(true);

        var result = options.UseKeyInputMode(KeyInputMode.Value);

        result.Logger.Should().BeSameAs(logger);
        result.BlobStorageProvider.Should().BeSameAs(blobProvider);
        result.FieldEncryptor.Should().BeSameAs(encryptor);
        result.DefaultConsistentRead.Should().Be(true);
        result.DefaultKeyInputMode.Should().Be(KeyInputMode.Value);
    }

    #endregion

    #region Existing Options Methods Still Work (Requirement 6.5)

    [Fact]
    public void WithLogger_StillWorksAfterUseKeyInputMode()
    {
        var logger = Substitute.For<IDynamoDbLogger>();

        var options = new FluentDynamoDbOptions()
            .UseKeyInputMode(KeyInputMode.Raw)
            .WithLogger(logger);

        options.Logger.Should().BeSameAs(logger);
        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Raw);
    }

    [Fact]
    public void WithBlobStorage_StillWorksAfterUseKeyInputMode()
    {
        var provider = Substitute.For<IBlobStorageProvider>();

        var options = new FluentDynamoDbOptions()
            .UseKeyInputMode(KeyInputMode.Value)
            .WithBlobStorage(provider);

        options.BlobStorageProvider.Should().BeSameAs(provider);
        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Value);
    }

    [Fact]
    public void WithEncryption_StillWorksAfterUseKeyInputMode()
    {
        var encryptor = Substitute.For<IFieldEncryptor>();

        var options = new FluentDynamoDbOptions()
            .UseKeyInputMode(KeyInputMode.Raw)
            .WithEncryption(encryptor);

        options.FieldEncryptor.Should().BeSameAs(encryptor);
        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Raw);
    }

    [Fact]
    public void UseConsistentRead_StillWorksAfterUseKeyInputMode()
    {
        var options = new FluentDynamoDbOptions()
            .UseKeyInputMode(KeyInputMode.Value)
            .UseConsistentRead(true);

        options.DefaultConsistentRead.Should().Be(true);
        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Value);
    }

    [Fact]
    public void FullChain_AllMethodsWorkTogether()
    {
        var logger = Substitute.For<IDynamoDbLogger>();
        var blobProvider = Substitute.For<IBlobStorageProvider>();
        var encryptor = Substitute.For<IFieldEncryptor>();

        var options = new FluentDynamoDbOptions()
            .WithLogger(logger)
            .WithBlobStorage(blobProvider)
            .WithEncryption(encryptor)
            .UseConsistentRead(true)
            .UseKeyInputMode(KeyInputMode.Raw);

        options.Logger.Should().BeSameAs(logger);
        options.BlobStorageProvider.Should().BeSameAs(blobProvider);
        options.FieldEncryptor.Should().BeSameAs(encryptor);
        options.DefaultConsistentRead.Should().Be(true);
        options.DefaultKeyInputMode.Should().Be(KeyInputMode.Raw);
    }

    #endregion
}
