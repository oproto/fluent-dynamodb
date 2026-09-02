using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that the default KeyInputMode (backward compatibility)
/// preserves pre-prefixed values from Entity.Keys.Pk(...) without modification.
/// When no KeyInputMode is specified, the default resolves to Auto, which detects
/// that the value already starts with the configured prefix and passes it through unchanged.
/// Validates: Requirement 14.5 — Default KeyInputMode backward compatibility
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "KeyInputMode")]
public class KeyInputModeDefaultBackwardCompatibilityTests
{
    [Fact]
    public async Task Get_WithDefaultMode_PrePrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        // Use Entity.Keys.Pk("12345") which returns "ORDER#12345"
        var prePrefixedPk = PrefixedKeyTestEntity.Keys.Pk("12345");
        var skValue = "some-sort-key";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — invoke accessor WITHOUT specifying KeyInputMode (uses the default)
        await table.PrefixedKeyTestEntitys.Get(prePrefixedPk, skValue).GetItemAsync();

        // Assert — key value should be "ORDER#12345" unchanged (Auto detects existing prefix)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == skValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithDefaultMode_PrePrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var prePrefixedPk = PrefixedKeyTestEntity.Keys.Pk("12345");
        var skValue = "some-sort-key";

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act — invoke delete WITHOUT specifying KeyInputMode
        await table.PrefixedKeyTestEntitys.Delete(prePrefixedPk, skValue).DeleteAsync();

        // Assert — key value should be "ORDER#12345" unchanged
        await mockClient.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == skValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithDefaultMode_PrePrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var prePrefixedPk = PrefixedKeyTestEntity.Keys.Pk("12345");
        var skValue = "some-sort-key";

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act — invoke update WITHOUT specifying KeyInputMode
        await table.PrefixedKeyTestEntitys.Update(prePrefixedPk, skValue)
            .Set("SET #status = :s")
            .WithAttribute("#status", "status")
            .WithValue(":s", "active")
            .UpdateAsync();

        // Assert — key value should be "ORDER#12345" unchanged
        await mockClient.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == skValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WithDefaultMode_PrePrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var prePrefixedPk = PrefixedKeyTestEntity.Keys.Pk("12345");
        var skValue = "some-sort-key";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use the convenience GetAsync method WITHOUT specifying KeyInputMode
        await table.PrefixedKeyTestEntitys.GetAsync(prePrefixedPk, skValue);

        // Assert — key value should be "ORDER#12345" unchanged
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == skValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TableLevel_Get_WithDefaultMode_PrePrefixedValuePassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var prePrefixedPk = PrefixedKeyTestEntity.Keys.Pk("12345");
        var skValue = "some-sort-key";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use table-level Get (delegates to entity accessor) without KeyInputMode
        await table.Get(prePrefixedPk, skValue).GetItemAsync();

        // Assert — key value should be "ORDER#12345" unchanged
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == skValue),
            Arg.Any<CancellationToken>());
    }
}
