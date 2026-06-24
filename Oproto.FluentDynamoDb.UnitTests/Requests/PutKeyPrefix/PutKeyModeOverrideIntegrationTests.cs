using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.PutKeyPrefix;

/// <summary>
/// Integration tests verifying that explicit KeyInputMode overrides on the PutItemRequestBuilder
/// propagate correctly through serialization and produce the expected key values in the
/// PutItemRequest sent to the DynamoDB client.
/// 
/// Validates: Requirements 4.4, 4.5, 4.6, 5.2, 5.4, 8.2, 8.4
/// </summary>
[Collection("OperationContext")]
public class PutKeyModeOverrideIntegrationTests
{
    private const string Prefix = "ORDER";
    private const string Separator = "#";
    private const string SkPrefix = "ITEM";
    private const string TableName = "test-orders";

    /// <summary>
    /// Test entity that simulates source-generated ToDynamoDb behavior with key prefix application.
    /// The ToDynamoDb overload with KeyInputMode applies KeyPrefixHelper just like the generated code would.
    /// </summary>
    private class PrefixedKeyEntity : IDynamoDbEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            // Existing overload delegates to new one with Default mode
            return ToDynamoDb(entity, options, KeyInputMode.Default);
        }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity
        {
            var typedEntity = (PrefixedKeyEntity)(object)entity;
            var resolvedMode = KeyInputModeResolver.Resolve(keyInputMode, options ?? new FluentDynamoDbOptions());

            // Simulate source-generated code: apply prefix to pk and sk
            var pkValue = KeyPrefixHelper.ApplyKeyPrefix(typedEntity.Pk, Prefix, Separator, resolvedMode);
            var skValue = KeyPrefixHelper.ApplyKeyPrefix(typedEntity.Sk, SkPrefix, Separator, resolvedMode);

            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pkValue },
                ["sk"] = new AttributeValue { S = skValue },
                ["name"] = new AttributeValue { S = typedEntity.Name }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
            where TSelf : IReadOnlyEntity
        {
            var entity = new PrefixedKeyEntity
            {
                Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Sk = item.TryGetValue("sk", out var sk) ? sk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
            => FromDynamoDb<TSelf>(items.First(), options);

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
            => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
            => item.ContainsKey("pk") && item.ContainsKey("sk");

        public static bool RequiresWriteTransaction => false;

        public static EntityMetadata GetEntityMetadata() => new()
        {
            TableName = TableName,
            Properties = new[]
            {
                new PropertyMetadata { PropertyName = "Pk", AttributeName = "pk", IsPartitionKey = true },
                new PropertyMetadata { PropertyName = "Sk", AttributeName = "sk", IsSortKey = true },
                new PropertyMetadata { PropertyName = "Name", AttributeName = "name" }
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

    private readonly IAmazonDynamoDB _mockClient;
    private PutItemRequest? _capturedRequest;

    public PutKeyModeOverrideIntegrationTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
        _mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _capturedRequest = callInfo.Arg<PutItemRequest>();
                return new PutItemResponse();
            });
    }

    #region WithKeyMode(KeyInputMode.Raw) — raw values pass through

    [Fact]
    public async Task WithKeyMode_Raw_MockClientReceivesRawUnprefixedValues()
    {
        // Arrange
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test Order" };
        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient)
            .ForTable(TableName)
            .WithKeyMode(KeyInputMode.Raw)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — key values should pass through unchanged (no prefix prepended)
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("12345");
        _capturedRequest.Item["sk"].S.Should().Be("abc");
        _capturedRequest.Item["name"].S.Should().Be("Test Order");
    }

    [Fact]
    public async Task WithKeyMode_Raw_AlreadyPrefixedValues_PassThroughUnchanged()
    {
        // Arrange — even if the value already has the prefix, Raw mode doesn't strip it
        var entity = new PrefixedKeyEntity { Pk = "ORDER#12345", Sk = "ITEM#abc", Name = "Test" };
        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient)
            .ForTable(TableName)
            .WithKeyMode(KeyInputMode.Raw)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — values pass through as-is
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("ORDER#12345");
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#abc");
    }

    #endregion

    #region WithKeyMode(KeyInputMode.Value) — always prefixed

    [Fact]
    public async Task WithKeyMode_Value_MockClientReceivesAlwaysPrefixedValues()
    {
        // Arrange
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test Order" };
        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient)
            .ForTable(TableName)
            .WithKeyMode(KeyInputMode.Value)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — prefix+separator is always prepended
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("ORDER#12345");
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#abc");
    }

    [Fact]
    public async Task WithKeyMode_Value_AlreadyPrefixedValues_DoublePrefixes()
    {
        // Arrange — Value mode always prepends, even if prefix is already present
        var entity = new PrefixedKeyEntity { Pk = "ORDER#12345", Sk = "ITEM#abc", Name = "Test" };
        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient)
            .ForTable(TableName)
            .WithKeyMode(KeyInputMode.Value)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — prefix is prepended again (double-prefix is expected in Value mode)
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("ORDER#ORDER#12345");
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#ITEM#abc");
    }

    #endregion

    #region PutAsync convenience method — uses options default

    [Fact]
    public async Task PutAsync_ConvenienceMethod_UsesDefaultKeyInputModeFromOptions()
    {
        // Arrange — default FluentDynamoDbOptions has DefaultKeyInputMode = Auto
        var options = new FluentDynamoDbOptions();
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test" };

        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient, options)
            .ForTable(TableName)
            .WithItem(entity);

        // Act — calling PutAsync without explicit WithKeyMode
        await builder.PutAsync();

        // Assert — Auto mode: value doesn't have prefix, so prefix gets prepended
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("ORDER#12345");
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#abc");
    }

    [Fact]
    public async Task PutAsync_ConvenienceMethod_WithOptionsDefaultRaw_PassesValuesUnchanged()
    {
        // Arrange — configure options to use Raw as the default mode
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test" };

        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient, options)
            .ForTable(TableName)
            .WithItem(entity);

        // Act — no explicit WithKeyMode, so resolves Default → options.DefaultKeyInputMode (Raw)
        await builder.PutAsync();

        // Assert — Raw mode from options: values pass through unchanged
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("12345");
        _capturedRequest.Item["sk"].S.Should().Be("abc");
    }

    [Fact]
    public async Task PutAsync_ConvenienceMethod_WithOptionsDefaultValue_AlwaysPrepends()
    {
        // Arrange — configure options to use Value as the default mode
        var options = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Value);
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test" };

        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient, options)
            .ForTable(TableName)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — Value mode from options: prefix always prepended
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("ORDER#12345");
        _capturedRequest.Item["sk"].S.Should().Be("ITEM#abc");
    }

    [Fact]
    public async Task PutAsync_ExplicitWithKeyMode_OverridesOptionsDefault()
    {
        // Arrange — options says Auto, but per-call override says Raw
        var options = new FluentDynamoDbOptions(); // Default = Auto
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "abc", Name = "Test" };

        var builder = new PutItemRequestBuilder<PrefixedKeyEntity>(_mockClient, options)
            .ForTable(TableName)
            .WithKeyMode(KeyInputMode.Raw)
            .WithItem(entity);

        // Act
        await builder.PutAsync();

        // Assert — per-call Raw overrides the options Auto default
        _capturedRequest.Should().NotBeNull();
        _capturedRequest!.Item["pk"].S.Should().Be("12345");
        _capturedRequest.Item["sk"].S.Should().Be("abc");
    }

    #endregion
}
