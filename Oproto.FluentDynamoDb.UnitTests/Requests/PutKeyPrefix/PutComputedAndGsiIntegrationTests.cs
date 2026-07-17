using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.UnitTests.Properties;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.PutKeyPrefix;

/// <summary>
/// Integration tests for computed key exclusion and GSI/LSI key prefix application during Put.
/// 
/// Validates:
///   - Computed PK excluded from prefix, non-computed SK gets prefix (Req 3.1, 3.2)
///   - Non-computed PK gets prefix, computed SK passes through unchanged (Req 3.3)
///   - GSI key carrying primary key prefix attribute gets prefix applied (Req 10.1, 10.2, 10.3)
///   - GSI key without primary key prefix attribute passes through unchanged (Req 10.4, 10.5)
///   - Hydrator path receives and applies KeyInputMode (Req 7.3)
/// 
/// Requirements: 3.1, 3.2, 3.3, 10.1, 10.2, 10.3, 10.4, 10.5, 7.3
/// </summary>
[Collection("OperationContext")]
public class PutComputedAndGsiIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient;
    private PutItemRequest? _capturedRequest;

    public PutComputedAndGsiIntegrationTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _capturedRequest = callInfo.Arg<PutItemRequest>();
                return new PutItemResponse();
            });
    }

    #region Computed PK + non-computed SK: only SK gets prefix

    /// <summary>
    /// When entity has a computed PK (with prefix configured) and a non-computed SK with prefix,
    /// only the SK gets prefix applied. The computed PK value passes through as-is.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task PutAsync_ComputedPk_NonComputedSk_OnlySkGetsPrefix()
    {
        // Arrange — ComputedPkWithPrefixTestEntity has:
        //   PK: [PartitionKey(Prefix = "EVT")] + [Computed("Component1", "Component2", Separator = "#")]
        //   SK: [SortKey(Prefix = "META")]
        var entity = new ComputedPkWithPrefixTestEntity
        {
            Component1 = "2024",
            Component2 = "event1",
            Sk = "details"
        };

        var builder = new PutItemRequestBuilder<ComputedPkWithPrefixTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Computed PK should NOT have "EVT#" prefix, SK should get "META#" prefix
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("2024#event1"); // Computed value, no prefix
        _capturedRequest.Item["sk"].S.Should().Be("META#details"); // Non-computed, gets prefix
    }

    /// <summary>
    /// When entity has a computed PK and explicit Raw mode is used, 
    /// neither PK nor SK get prefix applied (Raw mode passes everything through).
    /// The computed key exclusion is mode-independent, but Raw mode additionally
    /// prevents prefix on non-computed keys.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task PutAsync_ComputedPk_WithRawMode_NothingGetsPrefix()
    {
        // Arrange
        var entity = new ComputedPkWithPrefixTestEntity
        {
            Component1 = "2024",
            Component2 = "event1",
            Sk = "details"
        };

        var builder = new PutItemRequestBuilder<ComputedPkWithPrefixTestEntity>(_mockClient);
        builder.ForTable("test-table").WithKeyMode(KeyInputMode.Raw).WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Neither key gets prefix in Raw mode
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("2024#event1"); // Computed value, never prefixed
        _capturedRequest.Item["sk"].S.Should().Be("details"); // Raw mode, no prefix
    }

    /// <summary>
    /// When entity has a computed PK and Value mode is used,
    /// the computed PK still does not get prefix (exclusion rule applies regardless of mode),
    /// while the non-computed SK always gets prefix prepended.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task PutAsync_ComputedPk_WithValueMode_OnlySkGetsPrefix()
    {
        // Arrange
        var entity = new ComputedPkWithPrefixTestEntity
        {
            Component1 = "2024",
            Component2 = "event1",
            Sk = "details"
        };

        var builder = new PutItemRequestBuilder<ComputedPkWithPrefixTestEntity>(_mockClient);
        builder.ForTable("test-table").WithKeyMode(KeyInputMode.Value).WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Computed PK never gets prefix, SK always gets prefix in Value mode
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("2024#event1"); // Computed, excluded
        _capturedRequest.Item["sk"].S.Should().Be("META#details"); // Value mode, always prepends
    }

    #endregion

    #region Non-computed PK + computed SK: only PK gets prefix

    /// <summary>
    /// When entity has a non-computed PK with prefix and a computed SK (no prefix configured),
    /// only the PK gets prefix applied. The computed SK value passes through unchanged.
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public async Task PutAsync_NonComputedPk_ComputedSk_OnlyPkGetsPrefix()
    {
        // Arrange — NonComputedPkComputedSkTestEntity has:
        //   PK: [PartitionKey(Prefix = "CUST")] — non-computed
        //   SK: [SortKey] + [Computed("Region", "City", Separator = "#")] — computed, no prefix
        var entity = new NonComputedPkComputedSkTestEntity
        {
            Pk = "customer123",
            Region = "US",
            City = "Seattle"
        };

        var builder = new PutItemRequestBuilder<NonComputedPkComputedSkTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Non-computed PK gets "CUST#" prefix, computed SK passes through unchanged
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("CUST#customer123"); // Non-computed, gets prefix
        _capturedRequest.Item["sk"].S.Should().Be("US#Seattle"); // Computed value, passes through unchanged
    }

    /// <summary>
    /// When entity has a non-computed PK with prefix and a computed SK (no prefix configured),
    /// using Value mode, only the PK gets prefix (computed SK passes through unchanged).
    /// Validates: Requirements 3.3
    /// </summary>
    [Fact]
    public async Task PutAsync_NonComputedPk_ComputedSk_ValueMode_OnlyPkGetsPrefix()
    {
        // Arrange
        var entity = new NonComputedPkComputedSkTestEntity
        {
            Pk = "customer123",
            Region = "US",
            City = "Seattle"
        };

        var builder = new PutItemRequestBuilder<NonComputedPkComputedSkTestEntity>(_mockClient);
        builder.ForTable("test-table").WithKeyMode(KeyInputMode.Value).WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — PK always gets prefix in Value mode, computed SK passes through unchanged
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("CUST#customer123"); // Value mode, always prepends
        _capturedRequest.Item["sk"].S.Should().Be("US#Seattle"); // Computed, passes through unchanged
    }

    #endregion

    #region GSI key with primary key prefix attribute: gets prefix

    /// <summary>
    /// When a property has both [GsiPartitionKey("gsi1")] and [PartitionKey(Prefix = "CATEGORY")],
    /// the source generator applies prefix to that property during Put serialization.
    /// Validates: Requirements 10.1, 10.2, 10.3
    /// </summary>
    [Fact]
    public async Task PutAsync_GsiKeyWithPrimaryKeyPrefix_GetsPrefixApplied()
    {
        // Arrange — GsiWithPrimaryKeyPrefixTestEntity has:
        //   PK (also GSI PK): [PartitionKey(Prefix = "CATEGORY")] + [GsiPartitionKey("gsi1")]
        //   SK: [SortKey(Prefix = "ITEM")]
        var entity = new GsiWithPrimaryKeyPrefixTestEntity
        {
            Pk = "electronics",
            Sk = "product123",
            Name = "Laptop"
        };

        var builder = new PutItemRequestBuilder<GsiWithPrimaryKeyPrefixTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — GSI key with primary key prefix gets prefix applied
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("CATEGORY#electronics"); // PK (and GSI PK) gets prefix
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#product123"); // SK gets prefix
    }

    /// <summary>
    /// When a GSI key property does NOT carry a [PartitionKey] or [SortKey] with prefix,
    /// the value passes through unchanged (no prefix applied to standalone GSI keys).
    /// Validates: Requirements 10.4
    /// </summary>
    [Fact]
    public async Task PutAsync_StandaloneGsiKey_NoPrimaryKeyPrefix_PassesThrough()
    {
        // Arrange — GsiStandaloneKeyTestEntity has:
        //   PK: [PartitionKey(Prefix = "USER")] — gets prefix
        //   SK: [SortKey(Prefix = "ORDER")] — gets prefix  
        //   GsiPk: [GsiPartitionKey("gsi1")] only (no PartitionKey/SortKey attribute) — no prefix
        var entity = new GsiStandaloneKeyTestEntity
        {
            Pk = "user1",
            Sk = "order1",
            GsiPk = "rawGsiValue",
            Name = "Test"
        };

        var builder = new PutItemRequestBuilder<GsiStandaloneKeyTestEntity>(_mockClient);
        builder.ForTable("test-table").WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — PK and SK get prefix, standalone GSI key does NOT get prefix
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("USER#user1"); // PK gets prefix
        _capturedRequest.Item["sk"].S.Should().Be("ORDER#order1"); // SK gets prefix
        _capturedRequest.Item["gsi1pk"].S.Should().Be("rawGsiValue"); // Standalone GSI, no prefix
    }

    #endregion

    #region Hydrator path: receives and applies KeyInputMode

    /// <summary>
    /// When an entity requires async serialization via hydrator (e.g., encrypted fields),
    /// the PutItemRequestBuilder defers serialization and delegates to the hydrator at execution time.
    /// The builder passes _keyInputMode to hydrator.SerializeAsync(entity, blob, options, keyInputMode, ct).
    /// 
    /// Due to .NET Default Interface Method (DIM) dispatch, the interface's default body for the
    /// 5-param SerializeAsync delegates to the 3-param overload. Source-generated hydrators properly
    /// override this. This test verifies:
    /// 1. Builder stores the specified KeyInputMode
    /// 2. Builder defers serialization when hydrator is registered  
    /// 3. Builder uses hydrator's output (not inline ToDynamoDb) for the request
    /// 
    /// Validates: Requirements 7.3
    /// </summary>
    [Fact]
    public async Task PutAsync_HydratorPath_DefersToHydratorAndPassesKeyInputMode()
    {
        // Arrange — use a concrete hydrator that produces distinguishable output
        var capturingHydrator = new CapturingHydrator();

        var registry = new DefaultEntityHydratorRegistry();
        registry.Register<HydratorPathTestEntity>(capturingHydrator);

        var options = new FluentDynamoDbOptions().WithHydratorRegistry(registry);

        var entity = new HydratorPathTestEntity
        {
            Pk = "value",
            Sk = "sort",
            EncryptedData = "secret"
        };

        var builder = new PutItemRequestBuilder<HydratorPathTestEntity>(_mockClient, options)
            .ForTable("test-table")
            .WithKeyMode(KeyInputMode.Value);

        builder.WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — builder stored the correct KeyInputMode
        builder.GetKeyInputMode().Should().Be(KeyInputMode.Value);

        // Assert — the hydrator was invoked (item came from hydrator, not inline ToDynamoDb)
        // The hydrator uses "VALUE_PREFIX" prefix, while inline ToDynamoDb uses "PREFIX"
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Contain("VALUE_PREFIX",
            "request item should come from hydrator (uses 'VALUE_PREFIX' prefix), not inline ToDynamoDb (uses 'PREFIX')");

        // Assert — hydrator's SerializeAsync was called
        var totalCalls = capturingHydrator.SerializeWithModeCallCount;
        totalCalls.Should().BeGreaterThan(0, "hydrator's SerializeAsync should have been called");
    }

    /// <summary>
    /// When an entity goes through the hydrator path with default mode,
    /// the builder stores KeyInputMode.Default and defers to hydrator.
    /// Validates: Requirements 7.3
    /// </summary>
    [Fact]
    public async Task PutAsync_HydratorPath_DefaultMode_DefersToHydrator()
    {
        // Arrange
        var capturingHydrator = new CapturingHydrator();

        var registry = new DefaultEntityHydratorRegistry();
        registry.Register<HydratorPathTestEntity>(capturingHydrator);

        var options = new FluentDynamoDbOptions().WithHydratorRegistry(registry);

        var entity = new HydratorPathTestEntity
        {
            Pk = "value",
            Sk = "sort",
            EncryptedData = "secret"
        };

        var builder = new PutItemRequestBuilder<HydratorPathTestEntity>(_mockClient, options)
            .ForTable("test-table");

        builder.WithItem(entity);

        // Act — no WithKeyMode call, uses Default
        await builder.PutAsync();

        // Assert — builder stored Default mode
        builder.GetKeyInputMode().Should().Be(KeyInputMode.Default);

        // Assert — the hydrator was invoked (item came from hydrator)
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Contain("VALUE_PREFIX",
            "request item should come from hydrator, not inline ToDynamoDb");

        // Assert — hydrator's SerializeAsync was called
        var totalCalls = capturingHydrator.SerializeWithModeCallCount;
        totalCalls.Should().BeGreaterThan(0, "hydrator's SerializeAsync should have been called");
    }

    #endregion
}

#region Test Entity Definitions

/// <summary>
/// Test entity with non-computed PK (with prefix) and computed SK (no prefix).
/// The source generator should:
/// - Apply prefix to the non-computed PK (normal prefix behavior)
/// - NOT apply prefix to the computed SK (computed values pass through unchanged)
/// 
/// PK: [PartitionKey(Prefix = "CUST")] — non-computed, gets prefix
/// SK: [SortKey] + [Computed("Region", "City", Separator = "#")] — computed, passes through unchanged
/// </summary>
[DynamoDbTable("test-noncomputed-pk-computed-sk")]
public partial class NonComputedPkComputedSkTestEntity
{
    [PartitionKey(Prefix = "CUST")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("Region", "City", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
    public string Region { get; set; } = string.Empty;

    [Extracted("Sk", 1)]
    public string City { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}

/// <summary>
/// Test entity where the PK property has both [PartitionKey(Prefix = "CATEGORY")] and [GsiPartitionKey("gsi1")].
/// The source generator should apply prefix to this property since it carries a [PartitionKey] with prefix.
/// The [GsiPartitionKey] attribute alone does not trigger prefix, but the co-located [PartitionKey] does.
/// </summary>
[DynamoDbTable("test-gsi-with-pk-prefix")]
public partial class GsiWithPrimaryKeyPrefixTestEntity
{
    [PartitionKey(Prefix = "CATEGORY")]
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ITEM")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test entity with a standalone GSI partition key (no co-located [PartitionKey] or [SortKey] attribute).
/// The source generator should NOT apply prefix to the standalone GSI key property.
/// The PK and SK have their own prefixes and get prefix applied normally.
/// </summary>
[DynamoDbTable("test-gsi-standalone")]
public partial class GsiStandaloneKeyTestEntity
{
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string GsiPk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test entity used for verifying the hydrator path receives KeyInputMode.
/// This entity is designed to simulate an entity with encrypted fields that requires
/// async serialization through IAsyncEntityHydrator.
/// Uses an inline IDynamoDbEntity implementation for test isolation.
/// </summary>
public class HydratorPathTestEntity : IDynamoDbEntity
{
    public string Pk { get; set; } = string.Empty;
    public string Sk { get; set; } = string.Empty;
    public string EncryptedData { get; set; } = string.Empty;

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        return ToDynamoDb(entity, options, KeyInputMode.Default);
    }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
        where TSelf : IDynamoDbEntity
    {
        var typedEntity = (HydratorPathTestEntity)(object)entity;
        var resolvedMode = KeyInputModeResolver.Resolve(keyInputMode, options ?? new FluentDynamoDbOptions());

        var pkValue = KeyPrefixHelper.ApplyKeyPrefix(typedEntity.Pk, "PREFIX", "#", resolvedMode);
        var skValue = KeyPrefixHelper.ApplyKeyPrefix(typedEntity.Sk, "SK", "#", resolvedMode);

        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pkValue },
            ["sk"] = new AttributeValue { S = skValue },
            ["data"] = new AttributeValue { S = typedEntity.EncryptedData }
        };
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity
    {
        var entity = new HydratorPathTestEntity
        {
            Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Sk = item.TryGetValue("sk", out var sk) ? sk.S : string.Empty,
            EncryptedData = item.TryGetValue("data", out var data) ? data.S : string.Empty
        };
        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
        => FromDynamoDb<TSelf>(items.First(), options);

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        => item.ContainsKey("pk") && item.ContainsKey("data");

    public static bool RequiresWriteTransaction => false;

    public static EntityMetadata GetEntityMetadata() => new()
    {
        TableName = "test-hydrator-path",
        Properties = new[]
        {
            new PropertyMetadata { PropertyName = "Pk", AttributeName = "pk", IsPartitionKey = true },
            new PropertyMetadata { PropertyName = "Sk", AttributeName = "sk", IsSortKey = true },
            new PropertyMetadata { PropertyName = "EncryptedData", AttributeName = "data" }
        },
        Indexes = Array.Empty<IndexMetadata>(),
        Relationships = Array.Empty<RelationshipMetadata>()
    };

    public static Task<TSelf> FromDynamoDbAsync<TSelf>(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider? blobProvider,
        IFieldEncryptor? fieldEncryptor,
        FluentDynamoDbOptions? options,
        CancellationToken cancellationToken) where TSelf : IDynamoDbEntity
        => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

/// <summary>
/// Concrete implementation of IAsyncEntityHydrator that captures the KeyInputMode argument.
/// Used to verify that PutItemRequestBuilder correctly passes KeyInputMode through to the hydrator,
/// working around NSubstitute's inability to intercept default interface method implementations.
/// </summary>
internal class CapturingHydrator : IAsyncEntityHydrator<HydratorPathTestEntity>
{
    public KeyInputMode? CapturedKeyInputMode { get; private set; }
    public int SerializeWithModeCallCount { get; private set; }

    public Task<HydratorPathTestEntity> HydrateAsync(
        Dictionary<string, AttributeValue> item,
        IBlobStorageProvider? blobProvider,
        FluentDynamoDbOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HydratorPathTestEntity());
    }

    public Task<HydratorPathTestEntity> HydrateAsync(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider? blobProvider,
        FluentDynamoDbOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HydratorPathTestEntity());
    }

    public Task<Dictionary<string, AttributeValue>> SerializeAsync(
        HydratorPathTestEntity entity,
        IBlobStorageProvider? blobProvider,
        FluentDynamoDbOptions? options = null,
        KeyInputMode keyInputMode = KeyInputMode.Default,
        CancellationToken cancellationToken = default)
    {
        CapturedKeyInputMode = keyInputMode;
        SerializeWithModeCallCount++;

        var resolvedMode = KeyInputModeResolver.Resolve(keyInputMode, options ?? new FluentDynamoDbOptions());
        return Task.FromResult(BuildItem(entity, resolvedMode));
    }

    private static Dictionary<string, AttributeValue> BuildItem(HydratorPathTestEntity entity, KeyInputMode resolvedMode)
    {
        var pkValue = KeyPrefixHelper.ApplyKeyPrefix(entity.Pk, "VALUE_PREFIX", "#", resolvedMode);
        var skValue = KeyPrefixHelper.ApplyKeyPrefix(entity.Sk, "VALUE_SK", "#", resolvedMode);

        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pkValue },
            ["sk"] = new AttributeValue { S = skValue },
            ["data"] = new AttributeValue { S = entity.EncryptedData }
        };
    }
}

#endregion
