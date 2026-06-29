using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that KeyInputMode.Raw passes key values through unchanged
/// to DynamoDB requests, even when the entity has a configured prefix.
/// Validates: Requirement 14.3 — KeyInputMode.Raw behavior
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "KeyInputMode")]
public class KeyInputModeRawIntegrationTests
{
    [Fact]
    public async Task Get_WithKeyInputModeRaw_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var rawValue = "rawValue123";
        var sortKeyValue = "sk-value";

        // Configure mock to return an empty response
        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — pass "rawValue123" with KeyInputMode.Raw on an entity that has prefix "ORDER"
        await table.PrefixedKeyTestEntitys.Get(rawValue, sortKeyValue, KeyInputMode.Raw).GetItemAsync();

        // Assert — key in request should be "rawValue123" (no prefix applied, value passes through unchanged)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == rawValue &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WithKeyInputModeRaw_PrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        // Even if the value already has the prefix, Raw mode should NOT strip or modify it
        var alreadyPrefixedValue = "ORDER#12345";
        var sortKeyValue = "metadata";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act
        await table.PrefixedKeyTestEntitys.Get(alreadyPrefixedValue, sortKeyValue, KeyInputMode.Raw).GetItemAsync();

        // Assert — value should pass through unchanged even if it contains the prefix
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == alreadyPrefixedValue &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithKeyInputModeRaw_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var rawValue = "rawValue123";
        var sortKeyValue = "sk-value";

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        await table.PrefixedKeyTestEntitys.Delete(rawValue, sortKeyValue, KeyInputMode.Raw).DeleteAsync();

        // Assert — key in request should be "rawValue123" (no prefix applied)
        await mockClient.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(req =>
                req.Key["pk"].S == rawValue &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithKeyInputModeRaw_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var rawValue = "rawValue123";
        var sortKeyValue = "sk-value";

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act
        await table.PrefixedKeyTestEntitys.Update(rawValue, sortKeyValue, mode: KeyInputMode.Raw)
            .Set("SET #status = :s")
            .WithAttribute("#status", "status")
            .WithValue(":s", "active")
            .UpdateAsync();

        // Assert — key in request should be "rawValue123" (no prefix applied)
        await mockClient.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(req =>
                req.Key["pk"].S == rawValue &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WithKeyInputModeRaw_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var rawValue = "rawValue123";
        var sortKeyValue = "sk-value";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use the convenience GetAsync method
        await table.PrefixedKeyTestEntitys.GetAsync(rawValue, sortKeyValue, KeyInputMode.Raw);

        // Assert — key in request should be "rawValue123" (no prefix applied)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == rawValue &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }
}
