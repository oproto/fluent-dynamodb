using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.UnitTests.TestHelpers;

/// <summary>
/// Helper class for creating FluentDynamoDbOptions instances in tests.
/// Provides common test configurations with mock services.
/// </summary>
public static class TestOptionsBuilder
{
    /// <summary>
    /// Creates default options with NoOpLogger and no optional providers.
    /// </summary>
    public static FluentDynamoDbOptions CreateDefault()
        => new FluentDynamoDbOptions();

    /// <summary>
    /// Creates options with a mock logger for testing logging behavior.
    /// </summary>
    public static FluentDynamoDbOptions CreateWithMockLogger()
        => new FluentDynamoDbOptions().WithLogger(CreateMockLogger());

    /// <summary>
    /// Creates options with a mock logger and returns both the options and the mock.
    /// </summary>
    public static (FluentDynamoDbOptions Options, IDynamoDbLogger MockLogger) CreateWithMockLoggerAndCapture()
    {
        var mockLogger = CreateMockLogger();
        var options = new FluentDynamoDbOptions().WithLogger(mockLogger);
        return (options, mockLogger);
    }

    /// <summary>
    /// Creates options with a mock blob storage provider.
    /// </summary>
    public static FluentDynamoDbOptions CreateWithMockBlobStorage()
        => new FluentDynamoDbOptions().WithBlobStorage(CreateMockBlobStorageProvider());

    /// <summary>
    /// Creates options with a mock blob storage provider and returns both.
    /// </summary>
    public static (FluentDynamoDbOptions Options, IBlobStorageProvider MockProvider) CreateWithMockBlobStorageAndCapture()
    {
        var mockProvider = CreateMockBlobStorageProvider();
        var options = new FluentDynamoDbOptions().WithBlobStorage(mockProvider);
        return (options, mockProvider);
    }

    /// <summary>
    /// Creates options with a mock field encryptor.
    /// </summary>
    public static FluentDynamoDbOptions CreateWithMockEncryption()
        => new FluentDynamoDbOptions().WithEncryption(CreateMockFieldEncryptor());

    /// <summary>
    /// Creates options with a mock field encryptor and returns both.
    /// </summary>
    public static (FluentDynamoDbOptions Options, IFieldEncryptor MockEncryptor) CreateWithMockEncryptionAndCapture()
    {
        var mockEncryptor = CreateMockFieldEncryptor();
        var options = new FluentDynamoDbOptions().WithEncryption(mockEncryptor);
        return (options, mockEncryptor);
    }

    /// <summary>
    /// Creates options with all mock services configured.
    /// </summary>
    public static FluentDynamoDbOptions CreateWithAllMocks()
        => new FluentDynamoDbOptions()
            .WithLogger(CreateMockLogger())
            .WithBlobStorage(CreateMockBlobStorageProvider())
            .WithEncryption(CreateMockFieldEncryptor());

    /// <summary>
    /// Creates options with all mock services and returns all mocks.
    /// </summary>
    public static (FluentDynamoDbOptions Options, IDynamoDbLogger MockLogger, IBlobStorageProvider MockBlobProvider, IFieldEncryptor MockEncryptor) CreateWithAllMocksAndCapture()
    {
        var mockLogger = CreateMockLogger();
        var mockBlobProvider = CreateMockBlobStorageProvider();
        var mockEncryptor = CreateMockFieldEncryptor();
        
        var options = new FluentDynamoDbOptions()
            .WithLogger(mockLogger)
            .WithBlobStorage(mockBlobProvider)
            .WithEncryption(mockEncryptor);
        
        return (options, mockLogger, mockBlobProvider, mockEncryptor);
    }

    /// <summary>
    /// Creates a mock IDynamoDbLogger.
    /// </summary>
    public static IDynamoDbLogger CreateMockLogger()
        => Substitute.For<IDynamoDbLogger>();

    /// <summary>
    /// Creates a mock IBlobStorageProvider.
    /// </summary>
    public static IBlobStorageProvider CreateMockBlobStorageProvider()
        => Substitute.For<IBlobStorageProvider>();

    /// <summary>
    /// Creates a mock IFieldEncryptor.
    /// </summary>
    public static IFieldEncryptor CreateMockFieldEncryptor()
        => Substitute.For<IFieldEncryptor>();

    /// <summary>
    /// Creates a mock IGeospatialProvider.
    /// </summary>
    public static IGeospatialProvider CreateMockGeospatialProvider()
        => Substitute.For<IGeospatialProvider>();
}
