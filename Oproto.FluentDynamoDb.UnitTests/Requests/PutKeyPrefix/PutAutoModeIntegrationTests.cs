using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.UnitTests.MetadataTests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.PutKeyPrefix;

/// <summary>
/// Integration tests for Put operations with Auto mode (default KeyInputMode).
/// Verifies end-to-end that the mock DynamoDB client receives correctly prefixed key values
/// when entities with configured key prefixes are Put.
/// 
/// Uses KeyFormatTestEntity which has:
///   - PK prefix = "TEST", separator = "#"
///   - SK prefix = "SK", separator = "#"
///
/// Requirements: 1.2, 1.3, 2.2, 2.3, 8.1
/// </summary>
[Collection("OperationContext")]
public class PutAutoModeIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient;

    public PutAutoModeIntegrationTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());
    }

    /// <summary>
    /// When entity has raw PK and SK values (not already prefixed),
    /// Auto mode prepends the configured prefix+separator to both keys.
    /// Validates: Requirements 1.2, 2.2
    /// </summary>
    [Fact]
    public async Task PutAsync_WithRawValues_AutoModePrependsPrefix()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var entity = new KeyFormatTestEntity
        {
            Pk = "12345",
            Sk = "sortValue",
            Name = "TestEntity"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Auto mode should prepend prefix to raw values
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["pk"].S.Should().Be("TEST#12345");
        capturedRequest.Item["sk"].S.Should().Be("SK#sortValue");
        capturedRequest.Item["name"].S.Should().Be("TestEntity");
    }

    /// <summary>
    /// When entity PK value is already prefixed (e.g., via Entity.Keys.Pk(value)),
    /// Auto mode detects the existing prefix and passes through unchanged.
    /// This ensures backward compatibility with existing code using Keys.Pk().
    /// Validates: Requirements 1.3, 8.1
    /// </summary>
    [Fact]
    public async Task PutAsync_WithAlreadyPrefixedPk_AutoModePassesThroughUnchanged()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Simulate using Entity.Keys.Pk(value) which produces "TEST#12345"
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#12345",
            Sk = "SK#sortValue",
            Name = "TestEntity"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Auto mode should detect prefix is already present and pass through
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["pk"].S.Should().Be("TEST#12345");
        capturedRequest.Item["sk"].S.Should().Be("SK#sortValue");
    }

    /// <summary>
    /// When entity has both PK and SK with raw values,
    /// Auto mode prepends the correct prefix+separator to each key independently.
    /// Validates: Requirements 1.2, 2.2
    /// </summary>
    [Fact]
    public async Task PutAsync_WithBothRawPkAndSk_AutoModePrefixesBoth()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var entity = new KeyFormatTestEntity
        {
            Pk = "rawPkValue",
            Sk = "rawSkValue",
            Name = "BothRaw"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — both keys get their respective prefixes
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["pk"].S.Should().Be("TEST#rawPkValue");
        capturedRequest!.Item["sk"].S.Should().Be("SK#rawSkValue");
    }

    /// <summary>
    /// When entity has PK already prefixed but SK is raw,
    /// Auto mode correctly handles each key independently.
    /// Validates: Requirements 1.3, 2.2
    /// </summary>
    [Fact]
    public async Task PutAsync_WithPrefixedPkAndRawSk_AutoModeHandlesIndependently()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#alreadyPrefixed",
            Sk = "rawSort",
            Name = "MixedKeys"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — PK passes through (already prefixed), SK gets prefix prepended
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["pk"].S.Should().Be("TEST#alreadyPrefixed");
        capturedRequest!.Item["sk"].S.Should().Be("SK#rawSort");
    }

    /// <summary>
    /// Verifies that non-key properties are not affected by prefix application.
    /// Validates: Requirements 1.2, 2.2
    /// </summary>
    [Fact]
    public async Task PutAsync_NonKeyProperties_AreNotAffectedByPrefixApplication()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var entity = new KeyFormatTestEntity
        {
            Pk = "myId",
            Sk = "mySort",
            Name = "TEST#ShouldNotBePrefixed"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Name property should remain unchanged (it's not a key)
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["name"].S.Should().Be("TEST#ShouldNotBePrefixed");
    }

    /// <summary>
    /// Verifies that Auto mode (default) is used when no explicit KeyInputMode is set.
    /// This is the default behavior for Put(entity).PutAsync().
    /// Validates: Requirements 1.2, 2.2, 2.3
    /// </summary>
    [Fact]
    public async Task PutAsync_DefaultMode_ResolvesToAuto()
    {
        // Arrange
        PutItemRequest? capturedRequest = null;
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var entity = new KeyFormatTestEntity
        {
            Pk = "SK#value", // Starts with "SK#" but PK prefix is "TEST" — should get prefix prepended
            Sk = "TEST#value", // Starts with "TEST#" but SK prefix is "SK" — should get prefix prepended
            Name = "DefaultModeTest"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act — no WithKeyMode call, uses Default which resolves to Auto
        await builder.PutAsync();

        // Assert — values don't match their own prefix pattern, so prefix is prepended
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item["pk"].S.Should().Be("TEST#SK#value");
        capturedRequest!.Item["sk"].S.Should().Be("SK#TEST#value");
    }
}
