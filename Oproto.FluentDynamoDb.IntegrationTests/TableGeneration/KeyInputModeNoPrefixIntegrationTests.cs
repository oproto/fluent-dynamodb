using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that no key transformation is applied when the entity's key
/// has no configured prefix, regardless of any mode logic. Since no prefix means no KeyInputMode
/// parameter is generated, these tests verify the standard string accessor passes values unchanged.
/// Validates: Requirement 14.6 — No-prefix key with KeyInputMode (no transformation regardless of mode)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "KeyInputMode")]
public class KeyInputModeNoPrefixIntegrationTests
{
    [Fact]
    public async Task Get_NoPrefixEntity_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        // ComputedPkOnlyEvent has NO prefix on its PK — value should pass through unchanged
        var rawPkValue = "2024#12#25";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use standard string overload (no KeyInputMode parameter exists since no prefix)
        await table.ComputedPkOnlyEvents.Get(rawPkValue).GetItemAsync();

        // Assert — key in request should be identical to input (no transformation applied)
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == rawPkValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_NoPrefixEntity_ArbitraryStringPassesThroughUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        // Even a value that looks like it has a prefix should pass through unchanged
        // since the entity has no prefix configured
        var arbitraryValue = "PREFIX#somevalue";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act
        await table.ComputedPkOnlyEvents.Get(arbitraryValue).GetItemAsync();

        // Assert — value passes through unchanged because no prefix is configured
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == arbitraryValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_NoPrefixEntity_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        var rawPkValue = "2024#12#25";

        mockClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        await table.ComputedPkOnlyEvents.Delete(rawPkValue).DeleteAsync();

        // Assert — key value passes through unchanged
        await mockClient.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(req =>
                req.Key["pk"].S == rawPkValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_NoPrefixEntity_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        var rawPkValue = "2024#12#25";

        mockClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act
        await table.ComputedPkOnlyEvents.Update(rawPkValue)
            .Set("SET #title = :t")
            .WithAttribute("#title", "title")
            .WithValue(":t", "Test Event")
            .UpdateAsync();

        // Assert — key value passes through unchanged
        await mockClient.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(req =>
                req.Key["pk"].S == rawPkValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_NoPrefixEntity_ShouldPassKeyValueUnchanged()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        var rawPkValue = "2024#12#25";

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use the convenience GetAsync method
        await table.ComputedPkOnlyEvents.GetAsync(rawPkValue);

        // Assert — key value passes through unchanged
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == rawPkValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TypedOverload_NoPrefixEntity_KeyComposedWithoutAnyPrefixTransformation()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-computed-pk-only");

        // Use typed overload which composes key via Keys.Pk()
        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new Dictionary<string, AttributeValue>() });

        // Act — use typed overload (year, month, day)
        await table.ComputedPkOnlyEvents.Get(2024, 12, 25).GetItemAsync();

        // Assert — composed key "2024#12#25" should be passed through unchanged (no prefix)
        var expectedKey = ComputedPkOnlyEvent.Keys.Pk(2024, 12, 25);
        await mockClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req =>
                req.Key["pk"].S == expectedKey),
            Arg.Any<CancellationToken>());
    }
}
