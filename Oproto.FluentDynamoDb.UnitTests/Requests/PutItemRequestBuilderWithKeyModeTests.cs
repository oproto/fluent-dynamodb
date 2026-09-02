using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Collection("OperationContext")]
public class PutItemRequestBuilderWithKeyModeTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty }
            };
        }

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
            where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }

        public static bool RequiresWriteTransaction => false;

        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity
            => Task.FromResult(FromDynamoDb<TSelf>(items, options));
    }

    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public void WithKeyModeReturnsSameInstance()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        var result = builder.WithKeyMode(KeyInputMode.Auto);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void DefaultKeyInputModeIsDefault()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        builder.GetKeyInputMode().Should().Be(KeyInputMode.Default);
    }

    [Theory]
    [InlineData(KeyInputMode.Auto)]
    [InlineData(KeyInputMode.Value)]
    [InlineData(KeyInputMode.Raw)]
    [InlineData(KeyInputMode.Default)]
    public void WithKeyModeStoresExplicitMode(KeyInputMode mode)
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        builder.WithKeyMode(mode);

        builder.GetKeyInputMode().Should().Be(mode);
    }

    [Fact]
    public void WithKeyModeRawPassesValueUnchanged()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        builder.WithKeyMode(KeyInputMode.Raw);

        // Verify that Raw mode is stored — the builder should not transform
        // or interpret the mode itself; it stores and propagates it.
        builder.GetKeyInputMode().Should().Be(KeyInputMode.Raw);
    }

    [Fact]
    public void WithKeyModeIsChainableWithOtherBuilderMethods()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        var result = builder
            .ForTable("test-table")
            .WithKeyMode(KeyInputMode.Value)
            .WithItem(new TestEntity { Id = "123" });

        result.Should().BeSameAs(builder);
        builder.GetKeyInputMode().Should().Be(KeyInputMode.Value);
    }

    [Fact]
    public void WithKeyModeLastCallWins()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);

        builder.WithKeyMode(KeyInputMode.Auto);
        builder.WithKeyMode(KeyInputMode.Raw);

        builder.GetKeyInputMode().Should().Be(KeyInputMode.Raw);
    }
}
