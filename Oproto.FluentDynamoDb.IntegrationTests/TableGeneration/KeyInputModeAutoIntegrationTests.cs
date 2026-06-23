using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that KeyInputMode.Auto correctly detects whether a prefix
/// is already present and avoids double-prefixing, or applies the prefix when missing.
/// Validates: Requirement 14.2 — KeyInputMode.Auto behavior
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "KeyInputMode")]
public class KeyInputModeAutoIntegrationTests
{
    [Fact]
    public async Task Get_WithAutoMode_ValueAlreadyPrefixed_ShouldNotDoublePrefixKey()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        // Value already starts with prefix "ORDER" + separator "#"
        var alreadyPrefixedValue = "ORDER#12345";
        var sortKeyValue = "sk-value";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — pass "ORDER#12345" with KeyInputMode.Auto
        await table.PrefixedKeyTestEntitys.Get(alreadyPrefixedValue, sortKeyValue, KeyInputMode.Auto).GetItemAsync();

        // Assert — key should remain "ORDER#12345" (no double-prefix like "ORDER#ORDER#12345")
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WithAutoMode_ValueNotPrefixed_ShouldApplyPrefix()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        // Value does NOT start with prefix — should get prefix applied
        var rawValue = "12345";
        var sortKeyValue = "sk-value";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — pass "12345" with KeyInputMode.Auto
        await table.PrefixedKeyTestEntitys.Get(rawValue, sortKeyValue, KeyInputMode.Auto).GetItemAsync();

        // Assert — key should become "ORDER#12345" (prefix applied)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }
}
