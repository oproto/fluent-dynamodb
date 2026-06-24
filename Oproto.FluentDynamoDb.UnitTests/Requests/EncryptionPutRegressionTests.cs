using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Providers.Encryption;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Regression tests for the encrypted entity Put pipeline bug.
///
/// Bug: All generated put paths for entities with [Encrypted] properties called sync
/// serialization (ToDynamoDb) which throws NotSupportedException. The fix ensures all
/// Put entry points delegate to PutItemRequestBuilder.WithItem(TEntity) which properly
/// defers serialization to async execution time.
///
/// These tests will fail if the bug regresses — i.e., if any Put code path calls
/// ToDynamoDb directly instead of going through the builder's deferred serialization.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Encryption")]
[Collection("OperationContext")]
public class EncryptionPutRegressionTests
{
    private readonly IAmazonDynamoDB _client;
    private readonly IFieldEncryptor _encryptor;
    private readonly FluentDynamoDbOptions _options;

    public EncryptionPutRegressionTests()
    {
        _client = Substitute.For<IAmazonDynamoDB>();
        _encryptor = Substitute.For<IFieldEncryptor>();
        _encryptor.EncryptAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<FieldEncryptionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<byte[]>(0)));

        _options = new FluentDynamoDbOptions().WithEncryption(_encryptor);
    }

    /// <summary>
    /// Regression: PutItemRequestBuilder.WithItem(encryptedEntity) must not throw.
    /// It should defer serialization (set HasDeferredEntity = true).
    /// </summary>
    [Fact]
    public void WithItem_EncryptedEntity_DefersSerializationInsteadOfThrowing()
    {
        // Arrange
        var entity = CreateTestEntity();
        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, _options);
        builder.ForTable("test-table");

        // Act — must not throw NotSupportedException
        builder.WithItem(entity);

        // Assert — serialization is deferred
        builder.HasDeferredEntity.Should().BeTrue(
            "WithItem should defer serialization for encrypted entities instead of calling ToDynamoDb synchronously");
    }

    /// <summary>
    /// Regression: EntityExecuteAsyncExtensions.WithItem(builder, encryptedEntity) must not throw.
    /// This is the extension method called by the generic PutAsync&lt;TEntity&gt; on the table class.
    /// </summary>
    [Fact]
    public void ExtensionWithItem_EncryptedEntity_DefersSerializationInsteadOfThrowing()
    {
        // Arrange
        var entity = CreateTestEntity();
        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, _options);
        builder.ForTable("test-table");

        // Act — must not throw NotSupportedException or DynamoDbMappingException
        var result = EntityExecuteAsyncExtensions.WithItem(builder, entity);

        // Assert — serialization is deferred
        result.HasDeferredEntity.Should().BeTrue(
            "Extension WithItem should delegate to the builder's instance method which defers for encrypted entities");
    }

    /// <summary>
    /// Regression: Full PutAsync pipeline resolves deferred encrypted entity via hydrator.
    /// This tests the end-to-end path: WithItem(entity) defers → PutAsync resolves via hydrator → SDK called.
    /// </summary>
    [Fact]
    public async Task PutAsync_EncryptedEntity_ResolvesViaHydratorAndCallsSdk()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Register the hydrator so PutAsync can resolve the deferred entity
        var hydrator = Substitute.For<IAsyncEntityHydrator<EncryptionOnlyTestEntity>>();
        var expectedItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk-001" },
            ["name"] = new AttributeValue { S = "Alice" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };
        hydrator.SerializeAsync(
            Arg.Any<EncryptionOnlyTestEntity>(),
            Arg.Any<Providers.BlobStorage.IBlobStorageProvider>(),
            Arg.Any<FluentDynamoDbOptions>(),
            Arg.Any<KeyInputMode>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedItem);

        var registry = new DefaultEntityHydratorRegistry();
        registry.Register(hydrator);
        var options = new FluentDynamoDbOptions().WithEncryption(_encryptor).WithHydratorRegistry(registry);

        _client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, options);
        builder.ForTable("test-table");
        builder.WithItem(entity);

        // Act — should resolve deferred entity and call SDK
        await EntityExecuteAsyncExtensions.PutAsync(builder, CancellationToken.None);

        // Assert — SDK was called with the hydrator-resolved item
        await _client.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(req =>
                req.TableName == "test-table" &&
                req.Item.ContainsKey("pk") &&
                req.Item["pk"].S == "test-pk-001"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Regression: Full PutAsync pipeline works via the extension method path
    /// (the path used by the generic table.PutAsync&lt;TEntity&gt;(entity)).
    /// </summary>
    [Fact]
    public async Task PutAsync_ViaExtensionWithItem_ResolvesAndCallsSdk()
    {
        // Arrange
        var entity = CreateTestEntity();

        var hydrator = Substitute.For<IAsyncEntityHydrator<EncryptionOnlyTestEntity>>();
        var expectedItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-pk-001" },
            ["name"] = new AttributeValue { S = "Alice" },
            ["ssn"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        };
        hydrator.SerializeAsync(
            Arg.Any<EncryptionOnlyTestEntity>(),
            Arg.Any<Providers.BlobStorage.IBlobStorageProvider>(),
            Arg.Any<FluentDynamoDbOptions>(),
            Arg.Any<KeyInputMode>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedItem);

        var registry = new DefaultEntityHydratorRegistry();
        registry.Register(hydrator);
        var options = new FluentDynamoDbOptions().WithEncryption(_encryptor).WithHydratorRegistry(registry);

        _client.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Simulate what the generated table.PutAsync<TEntity>(entity) does:
        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, options);
        builder.ForTable("test-table");
        var configured = EntityExecuteAsyncExtensions.WithItem(builder, entity);

        // Act
        await EntityExecuteAsyncExtensions.PutAsync(configured, CancellationToken.None);

        // Assert
        await _client.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(req => req.Item["pk"].S == "test-pk-001"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Regression: Non-encrypted entities continue to serialize synchronously (no regression on happy path).
    /// </summary>
    [Fact]
    public void WithItem_NonEncryptedEntity_SerializesSynchronously()
    {
        // Arrange
        var entity = new PlainTestEntity { Pk = "pk-001", Name = "Bob" };
        var builder = new PutItemRequestBuilder<PlainTestEntity>(_client);
        builder.ForTable("test-table");

        // Act
        builder.WithItem(entity);

        // Assert — item is serialized immediately, not deferred
        builder.HasDeferredEntity.Should().BeFalse(
            "Non-encrypted entities should serialize synchronously");
        var request = builder.ToPutItemRequest();
        request.Item.Should().ContainKey("pk");
        request.Item["pk"].S.Should().Be("pk-001");
    }

    /// <summary>
    /// Regression: Extension WithItem for non-encrypted entities still works synchronously.
    /// </summary>
    [Fact]
    public void ExtensionWithItem_NonEncryptedEntity_SerializesSynchronously()
    {
        // Arrange
        var entity = new PlainTestEntity { Pk = "pk-001", Name = "Bob" };
        var builder = new PutItemRequestBuilder<PlainTestEntity>(_client);
        builder.ForTable("test-table");

        // Act
        var result = EntityExecuteAsyncExtensions.WithItem(builder, entity);

        // Assert
        result.HasDeferredEntity.Should().BeFalse(
            "Non-encrypted entities should serialize synchronously via extension method");
        var request = result.ToPutItemRequest();
        request.Item["pk"].S.Should().Be("pk-001");
    }

    /// <summary>
    /// Regression: Calling ToPutItemRequest() on a deferred builder (before async resolution)
    /// throws InvalidOperationException with a helpful message, not NotSupportedException.
    /// </summary>
    [Fact]
    public void ToPutItemRequest_WithUnresolvedDeferredEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = CreateTestEntity();
        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, _options);
        builder.ForTable("test-table");
        builder.WithItem(entity);

        // Act & Assert — should throw helpful message, not NotSupportedException from ToDynamoDb
        var action = () => builder.ToPutItemRequest();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires async serialization*");
    }

    /// <summary>
    /// Regression: WithItem correctly detects registered hydrator and defers without
    /// even attempting ToDynamoDb (the fast path).
    /// </summary>
    [Fact]
    public void WithItem_WithRegisteredHydrator_DefersWithoutAttemptingToDynamoDb()
    {
        // Arrange — register hydrator so the fast path is taken
        var hydrator = Substitute.For<IAsyncEntityHydrator<EncryptionOnlyTestEntity>>();
        var registry = new DefaultEntityHydratorRegistry();
        registry.Register(hydrator);
        var options = new FluentDynamoDbOptions().WithEncryption(_encryptor).WithHydratorRegistry(registry);

        var entity = CreateTestEntity();
        var builder = new PutItemRequestBuilder<EncryptionOnlyTestEntity>(_client, options);
        builder.ForTable("test-table");

        // Act
        builder.WithItem(entity);

        // Assert — deferred via hydrator detection (fast path, no exception catching needed)
        builder.HasDeferredEntity.Should().BeTrue();
        builder.GetDeferredEntity().Should().BeSameAs(entity);
    }

    private static EncryptionOnlyTestEntity CreateTestEntity() => new()
    {
        Pk = "test-pk-001",
        Name = "Alice",
        SocialSecurityNumber = "123-45-6789"
    };
}
