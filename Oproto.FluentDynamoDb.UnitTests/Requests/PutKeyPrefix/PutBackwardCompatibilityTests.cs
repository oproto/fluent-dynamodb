using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.UnitTests.MetadataTests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.PutKeyPrefix;

/// <summary>
/// Backward compatibility integration tests verifying that:
/// 1. Existing code using Entity.Keys.Pk(value) continues to work in Auto mode
/// 2. FluentDynamoDbOptions.DefaultKeyInputMode = Raw passes all values unchanged
/// 3. Upgrading scenario: entity previously using raw values now gets prefix with Auto
/// 4. Per-call escape hatch: WithKeyMode(KeyInputMode.Raw) bypasses prefix in Auto mode
///
/// Uses KeyFormatTestEntity which has:
///   - PK prefix = "TEST", separator = "#"
///   - SK prefix = "SK", separator = "#"
///
/// Requirements: 8.1, 8.2, 8.3, 8.4
/// </summary>
[Collection("OperationContext")]
public class PutBackwardCompatibilityTests
{
    private readonly IAmazonDynamoDB _mockClient;
    private PutItemRequest? _capturedRequest;

    public PutBackwardCompatibilityTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
        _mockClient.PutItemAsync(Arg.Do<PutItemRequest>(req => _capturedRequest = req), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());
    }

    #region Existing code using Entity.Keys.Pk(value) continues to work in Auto mode (Requirement 8.1)

    /// <summary>
    /// Existing code that constructs prefixed keys via KeyFormatTestEntity.Keys.Pk(value)
    /// should continue to work — Auto mode detects the prefix is already present and passes through.
    /// Validates: Requirement 8.1
    /// </summary>
    [Fact]
    public async Task ExistingCode_UsingKeysPk_ContinuesToWorkInAutoMode()
    {
        // Arrange — simulate existing user code that creates keys with the Keys helper
        var prefixedPk = KeyFormatTestEntity.Keys.Pk("12345"); // Returns "TEST#12345"
        var prefixedSk = KeyFormatTestEntity.Keys.Sk("sortVal"); // Returns "SK#sortVal"

        var entity = new KeyFormatTestEntity
        {
            Pk = prefixedPk,
            Sk = prefixedSk,
            Name = "ExistingCodeEntity"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act — default mode is Auto
        await builder.PutAsync();

        // Assert — Auto mode sees "TEST#12345" starts with "TEST#" and passes through unchanged
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#12345");
        _capturedRequest.Item["sk"].S.Should().Be("SK#sortVal");
        _capturedRequest.Item["name"].S.Should().Be("ExistingCodeEntity");
    }

    /// <summary>
    /// When only the PK is constructed via Keys.Pk() and SK is also prefixed,
    /// both should pass through unchanged in Auto mode.
    /// Validates: Requirement 8.1
    /// </summary>
    [Fact]
    public async Task ExistingCode_BothKeysAlreadyPrefixed_AutoModePassesThroughBoth()
    {
        // Arrange
        var entity = new KeyFormatTestEntity
        {
            Pk = KeyFormatTestEntity.Keys.Pk("user-abc"),
            Sk = KeyFormatTestEntity.Keys.Sk("metadata"),
            Name = "BothPrefixed"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#user-abc");
        _capturedRequest.Item["sk"].S.Should().Be("SK#metadata");
    }

    /// <summary>
    /// Existing code using Keys.Pk with options explicitly set to Auto should still work.
    /// Validates: Requirement 8.1
    /// </summary>
    [Fact]
    public async Task ExistingCode_ExplicitAutoModeWithPrefixedKeys_PassesThrough()
    {
        // Arrange
        var options = new FluentDynamoDbOptions(); // Default is Auto
        var entity = new KeyFormatTestEntity
        {
            Pk = KeyFormatTestEntity.Keys.Pk("order-999"),
            Sk = KeyFormatTestEntity.Keys.Sk("line-1"),
            Name = "ExplicitAutoTest"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — still passes through since keys are already prefixed
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#order-999");
        _capturedRequest.Item["sk"].S.Should().Be("SK#line-1");
    }

    #endregion

    #region DefaultKeyInputMode = Raw passes all values unchanged (Requirement 8.2)

    /// <summary>
    /// When FluentDynamoDbOptions.DefaultKeyInputMode is set to Raw,
    /// all key values pass through unchanged regardless of prefix configuration.
    /// Validates: Requirement 8.2
    /// </summary>
    [Fact]
    public async Task RawModeGlobal_RawValues_PassThroughUnchanged()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);
        var entity = new KeyFormatTestEntity
        {
            Pk = "rawPkValue",
            Sk = "rawSkValue",
            Name = "RawModeTest"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Raw mode: no prefix applied, values pass through as-is
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("rawPkValue");
        _capturedRequest.Item["sk"].S.Should().Be("rawSkValue");
        _capturedRequest.Item["name"].S.Should().Be("RawModeTest");
    }

    /// <summary>
    /// When Raw mode is global default and values happen to already contain the prefix pattern,
    /// they are still passed through unchanged (Raw never strips or modifies).
    /// Validates: Requirement 8.2
    /// </summary>
    [Fact]
    public async Task RawModeGlobal_AlreadyPrefixedValues_StillPassThroughUnchanged()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#alreadyPrefixed",
            Sk = "SK#alreadySorted",
            Name = "RawWithPrefixed"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Raw mode doesn't modify anything even if it looks prefixed
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#alreadyPrefixed");
        _capturedRequest.Item["sk"].S.Should().Be("SK#alreadySorted");
    }

    /// <summary>
    /// Raw mode global setting matches legacy behavior — values stored exactly as provided.
    /// This is the migration path for users who don't want automatic prefixing.
    /// Validates: Requirement 8.2
    /// </summary>
    [Fact]
    public async Task RawModeGlobal_MatchesLegacyBehavior_NoTransformation()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);
        var entity = new KeyFormatTestEntity
        {
            Pk = "any-value-at-all",
            Sk = "completely-raw-sk",
            Name = "LegacyBehavior"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — values are exactly what was set, matching pre-feature legacy behavior
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("any-value-at-all");
        _capturedRequest.Item["sk"].S.Should().Be("completely-raw-sk");
    }

    #endregion

    #region Upgrading scenario: raw values now get prefix with Auto (Requirement 8.3)

    /// <summary>
    /// Simulates upgrade scenario: a user previously set raw values directly on entity keys
    /// (without calling Keys.Pk()) and relied on legacy no-prefix behavior.
    /// After upgrading to Auto mode (new default), those raw values now get prefix prepended.
    /// This is the expected behavior change that users should be aware of.
    /// Validates: Requirement 8.3
    /// </summary>
    [Fact]
    public async Task UpgradeScenario_PreviouslyRawValues_NowGetPrefixWithAutoMode()
    {
        // Arrange — user's old code set raw values directly (legacy pattern)
        var entity = new KeyFormatTestEntity
        {
            Pk = "12345",      // Previously stored as "12345" in legacy, now Auto will prepend "TEST#"
            Sk = "sortValue",  // Previously stored as "sortValue", now Auto will prepend "SK#"
            Name = "UpgradedEntity"
        };

        // Default options = Auto mode (new default behavior)
        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Auto mode prepends prefix because values don't start with prefix+separator
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#12345");
        _capturedRequest.Item["sk"].S.Should().Be("SK#sortValue");
    }

    /// <summary>
    /// Upgrade scenario where user previously stored values that coincidentally contain
    /// the prefix string but not at position 0 — Auto mode correctly identifies these
    /// as not prefixed and prepends the prefix.
    /// Validates: Requirement 8.3
    /// </summary>
    [Fact]
    public async Task UpgradeScenario_ValueContainsPrefixNotAtStart_GetsNewPrefix()
    {
        // Arrange — value contains "TEST" but not at the start
        var entity = new KeyFormatTestEntity
        {
            Pk = "myTEST#value",  // Contains "TEST#" but not at position 0
            Sk = "dataSK#item",   // Contains "SK#" but not at position 0
            Name = "ContainsPrefix"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Auto mode prepends because prefix+separator is not at start
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#myTEST#value");
        _capturedRequest.Item["sk"].S.Should().Be("SK#dataSK#item");
    }

    /// <summary>
    /// Upgrade scenario: user can opt out of the new behavior by setting Raw globally,
    /// allowing a gradual migration path.
    /// Validates: Requirements 8.2, 8.3
    /// </summary>
    [Fact]
    public async Task UpgradeScenario_OptOutWithRawGlobal_PreservesLegacyBehavior()
    {
        // Arrange — user opts out by setting Raw mode globally
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);
        var entity = new KeyFormatTestEntity
        {
            Pk = "12345",
            Sk = "sortValue",
            Name = "OptedOutEntity"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Raw mode preserves legacy behavior: no prefix applied
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("12345");
        _capturedRequest.Item["sk"].S.Should().Be("sortValue");
    }

    #endregion

    #region Per-call escape hatch: WithKeyMode(Raw) bypasses prefix in Auto mode (Requirement 8.4)

    /// <summary>
    /// User is in Auto mode globally but needs Raw for one specific call.
    /// WithKeyMode(KeyInputMode.Raw) should bypass prefix for that call only.
    /// Validates: Requirement 8.4
    /// </summary>
    [Fact]
    public async Task PerCallEscapeHatch_WithKeyModeRaw_BypassesPrefixInAutoGlobal()
    {
        // Arrange — global default is Auto (new FluentDynamoDbOptions())
        var options = new FluentDynamoDbOptions(); // Default = Auto
        var entity = new KeyFormatTestEntity
        {
            Pk = "rawValueForThisCall",
            Sk = "rawSortForThisCall",
            Name = "EscapeHatchTest"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table")
            .WithKeyMode(KeyInputMode.Raw) // Per-call override
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Raw mode overrides Auto for this call only
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("rawValueForThisCall");
        _capturedRequest.Item["sk"].S.Should().Be("rawSortForThisCall");
    }

    /// <summary>
    /// Verify that per-call Raw override works even with already-prefixed values
    /// (doesn't strip the prefix — Raw is truly pass-through).
    /// Validates: Requirement 8.4
    /// </summary>
    [Fact]
    public async Task PerCallEscapeHatch_WithKeyModeRaw_AlreadyPrefixedValuesPassThrough()
    {
        // Arrange
        var options = new FluentDynamoDbOptions(); // Default = Auto
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#alreadyHasPrefix",
            Sk = "SK#alreadyHasPrefix",
            Name = "PrefixedEscapeHatch"
        };

        var builder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        builder.ForTable("test-table")
            .WithKeyMode(KeyInputMode.Raw)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Raw pass-through: even prefixed values go unchanged
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#alreadyHasPrefix");
        _capturedRequest.Item["sk"].S.Should().Be("SK#alreadyHasPrefix");
    }

    /// <summary>
    /// Verify the escape hatch is truly per-call: a subsequent Put without WithKeyMode
    /// should revert to Auto mode behavior.
    /// Validates: Requirement 8.4
    /// </summary>
    [Fact]
    public async Task PerCallEscapeHatch_SubsequentCallWithoutOverride_RevertsToAuto()
    {
        // Arrange
        var options = new FluentDynamoDbOptions(); // Default = Auto
        var entity = new KeyFormatTestEntity
        {
            Pk = "rawValue",
            Sk = "rawSort",
            Name = "RevertTest"
        };

        // First call: Raw override (per-call)
        var rawBuilder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        rawBuilder.ForTable("test-table")
            .WithKeyMode(KeyInputMode.Raw)
            .WithItem(entity);
        await rawBuilder.PutAsync();

        // Verify Raw call worked
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("rawValue");

        // Reset captured request
        _capturedRequest = null;

        // Second call: no override — should use Auto (default)
        var autoBuilder = new PutItemRequestBuilder<KeyFormatTestEntity>(_mockClient, options);
        autoBuilder.ForTable("test-table")
            .WithItem(entity);
        await autoBuilder.PutAsync();

        // Assert — Auto mode prepends prefix for the second call
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("TEST#rawValue");
        _capturedRequest.Item["sk"].S.Should().Be("SK#rawSort");
    }

    #endregion
}
