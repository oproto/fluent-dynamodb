using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that KeyInputMode.Value always prepends the configured prefix
/// to the key value, regardless of whether the input already contains the prefix.
/// Validates: Requirement 14.4 — KeyInputMode.Value behavior
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "KeyInputMode")]
public class KeyInputModeValueIntegrationTests
{
    [Fact]
    public async Task Get_WithKeyInputModeValue_RawValue_ShouldPrependPrefix()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        var rawValue = "12345";
        var sortKeyValue = "sk-value";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — pass "12345" with KeyInputMode.Value on entity with prefix "ORDER"
        await table.PrefixedKeyTestEntitys.Get(rawValue, sortKeyValue, KeyInputMode.Value).GetItemAsync();

        // Assert — key in request should be "ORDER#12345" (prefix always prepended)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#12345" &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WithKeyInputModeValue_AlreadyPrefixedValue_ShouldDoublePrepend()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestPrefixedKeyTable(mockClient, "test-prefixed-key");

        // Value already contains the prefix — Value mode should STILL prepend it again
        var alreadyPrefixedValue = "ORDER#12345";
        var sortKeyValue = "metadata";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — pass "ORDER#12345" with KeyInputMode.Value on same entity
        await table.PrefixedKeyTestEntitys.Get(alreadyPrefixedValue, sortKeyValue, KeyInputMode.Value).GetItemAsync();

        // Assert — key should be "ORDER#ORDER#12345" (prefix ALWAYS prepended regardless of content)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == "ORDER#ORDER#12345" &&
                req.Key["sk"].S == sortKeyValue),
            Arg.Any<CancellationToken>());
    }
}
