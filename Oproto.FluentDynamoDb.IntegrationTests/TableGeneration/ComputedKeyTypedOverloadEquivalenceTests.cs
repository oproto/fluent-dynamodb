using NSubstitute;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests verifying that typed parameter overloads produce identical DynamoDB key values
/// as the standard overloads when invoked with manually-built keys via Entity.Keys.BuildPk/BuildSk.
/// Validates: Requirements 14.1, 3.5, 9.3
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "ComputedKeyOverloads")]
public class ComputedKeyTypedOverloadEquivalenceTests
{
    [Fact]
    public void ComputedPkOnly_TypedOverload_ProducesIdenticalKeysAsStandardOverload()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-table");

        int year = 2024;
        int month = 12;
        int day = 25;

        // Act - Typed overload path
        var typedBuilder = table.ComputedPkOnlyEvents.Get(year, month, day);
        var typedRequest = typedBuilder.ToGetItemRequest();

        // Act - Standard overload path with manually-built key
        var manualPk = ComputedPkOnlyEvent.Keys.BuildPk(year, month, day);
        var standardBuilder = table.ComputedPkOnlyEvents.Get(manualPk);
        var standardRequest = standardBuilder.ToGetItemRequest();

        // Assert - Key AttributeValue entries are identical
        typedRequest.Key.Should().NotBeNull();
        standardRequest.Key.Should().NotBeNull();

        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
    }

    [Fact]
    public void ComputedSkOnly_TypedOverload_ProducesIdenticalKeysAsStandardOverload()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedSkOnlyTable(mockClient, "test-table");

        string orderId = "ORD-12345";
        string region = "us-east";
        string category = "electronics";

        // Act - Typed overload path
        var typedBuilder = table.ComputedSkOnlyOrders.Get(orderId, region, category);
        var typedRequest = typedBuilder.ToGetItemRequest();

        // Act - Standard overload path with manually-built key
        var manualSk = ComputedSkOnlyOrder.Keys.BuildSk(region, category);
        var standardBuilder = table.ComputedSkOnlyOrders.Get(orderId, manualSk);
        var standardRequest = standardBuilder.ToGetItemRequest();

        // Assert - Both PK and SK AttributeValue entries are identical
        typedRequest.Key.Should().NotBeNull();
        standardRequest.Key.Should().NotBeNull();

        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }

    [Fact]
    public void ComputedBothKeys_TypedOverload_ProducesIdenticalKeysAsStandardOverload()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedBothKeysTable(mockClient, "test-table");

        string tenantId = "tenant-abc";
        string userId = "user-xyz";
        int year = 2024;
        int month = 6;

        // Act - Typed overload path
        var typedBuilder = table.ComputedBothKeysEntitys.Get(tenantId, userId, year, month);
        var typedRequest = typedBuilder.ToGetItemRequest();

        // Act - Standard overload path with manually-built keys
        var manualPk = ComputedBothKeysEntity.Keys.BuildPk(tenantId, userId);
        var manualSk = ComputedBothKeysEntity.Keys.BuildSk(year, month);
        var standardBuilder = table.ComputedBothKeysEntitys.Get(manualPk, manualSk);
        var standardRequest = standardBuilder.ToGetItemRequest();

        // Assert - Both PK and SK AttributeValue entries are identical
        typedRequest.Key.Should().NotBeNull();
        standardRequest.Key.Should().NotBeNull();

        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }

    [Fact]
    public void ComputedPkWithPrefix_TypedOverload_ProducesIdenticalKeysAsStandardOverload()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedWithPrefixTable(mockClient, "test-table");

        string tenantId = "tenant-123";
        string orderNum = "order-456";
        string sk = "metadata";

        // Act - Typed overload path
        var typedBuilder = table.ComputedWithPrefixEntitys.Get(tenantId, orderNum, sk);
        var typedRequest = typedBuilder.ToGetItemRequest();

        // Act - Standard overload path with manually-built key
        var manualPk = ComputedWithPrefixEntity.Keys.BuildPk(tenantId, orderNum);
        var standardBuilder = table.ComputedWithPrefixEntitys.Get(manualPk, sk);
        var standardRequest = standardBuilder.ToGetItemRequest();

        // Assert - Both PK and SK AttributeValue entries are identical
        typedRequest.Key.Should().NotBeNull();
        standardRequest.Key.Should().NotBeNull();

        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }

    [Fact]
    public async Task ComputedPkOnly_TypedOverload_CapturedRequestMatchesStandardOverload()
    {
        // Arrange - Use NSubstitute to capture the actual DynamoDB request
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-table");

        int year = 2025;
        int month = 1;
        int day = 15;

        GetItemRequest? capturedTypedRequest = null;
        GetItemRequest? capturedStandardRequest = null;

        // Act - Typed overload path: invoke and capture
        mockClient.GetItemAsync(Arg.Do<GetItemRequest>(req => capturedTypedRequest = req), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetItemResponse { Item = null }));

        await table.ComputedPkOnlyEvents.Get(year, month, day).GetItemAsync();

        // Reset the capture for the standard path
        mockClient.ClearReceivedCalls();

        mockClient.GetItemAsync(Arg.Do<GetItemRequest>(req => capturedStandardRequest = req), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetItemResponse { Item = null }));

        // Act - Standard overload path with manually-built key
        var manualPk = ComputedPkOnlyEvent.Keys.BuildPk(year, month, day);
        await table.ComputedPkOnlyEvents.Get(manualPk).GetItemAsync();

        // Assert - Captured requests have identical key entries
        capturedTypedRequest.Should().NotBeNull();
        capturedStandardRequest.Should().NotBeNull();

        capturedTypedRequest!.Key["pk"].S.Should().Be(capturedStandardRequest!.Key["pk"].S);
    }

    [Fact]
    public async Task ComputedSkOnly_TypedOverload_CapturedRequestMatchesStandardOverload()
    {
        // Arrange - Use NSubstitute to capture the actual DynamoDB request
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedSkOnlyTable(mockClient, "test-table");

        string orderId = "ORD-999";
        string region = "eu-west";
        string category = "books";

        GetItemRequest? capturedTypedRequest = null;
        GetItemRequest? capturedStandardRequest = null;

        // Act - Typed overload path
        mockClient.GetItemAsync(Arg.Do<GetItemRequest>(req => capturedTypedRequest = req), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetItemResponse { Item = null }));

        await table.ComputedSkOnlyOrders.Get(orderId, region, category).GetItemAsync();

        // Reset for the standard path
        mockClient.ClearReceivedCalls();

        mockClient.GetItemAsync(Arg.Do<GetItemRequest>(req => capturedStandardRequest = req), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetItemResponse { Item = null }));

        // Act - Standard overload path with manually-built key
        var manualSk = ComputedSkOnlyOrder.Keys.BuildSk(region, category);
        await table.ComputedSkOnlyOrders.Get(orderId, manualSk).GetItemAsync();

        // Assert - Captured requests have identical key entries
        capturedTypedRequest.Should().NotBeNull();
        capturedStandardRequest.Should().NotBeNull();

        capturedTypedRequest!.Key["pk"].S.Should().Be(capturedStandardRequest!.Key["pk"].S);
        capturedTypedRequest!.Key["sk"].S.Should().Be(capturedStandardRequest!.Key["sk"].S);
    }

    [Fact]
    public void ComputedPkOnly_Delete_TypedOverloadMatchesStandard()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-table");

        int year = 2023;
        int month = 7;
        int day = 4;

        // Act - Typed overload path
        var typedBuilder = table.ComputedPkOnlyEvents.Delete(year, month, day);
        var typedRequest = typedBuilder.ToDeleteItemRequest();

        // Act - Standard overload path
        var manualPk = ComputedPkOnlyEvent.Keys.BuildPk(year, month, day);
        var standardBuilder = table.ComputedPkOnlyEvents.Delete(manualPk);
        var standardRequest = standardBuilder.ToDeleteItemRequest();

        // Assert
        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
    }

    [Fact]
    public void ComputedSkOnly_Delete_TypedOverloadMatchesStandard()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedSkOnlyTable(mockClient, "test-table");

        string orderId = "ORD-555";
        string region = "ap-south";
        string category = "clothing";

        // Act - Typed overload path
        var typedBuilder = table.ComputedSkOnlyOrders.Delete(orderId, region, category);
        var typedRequest = typedBuilder.ToDeleteItemRequest();

        // Act - Standard overload path
        var manualSk = ComputedSkOnlyOrder.Keys.BuildSk(region, category);
        var standardBuilder = table.ComputedSkOnlyOrders.Delete(orderId, manualSk);
        var standardRequest = standardBuilder.ToDeleteItemRequest();

        // Assert
        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }

    [Fact]
    public void ComputedBothKeys_Delete_TypedOverloadMatchesStandard()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedBothKeysTable(mockClient, "test-table");

        string tenantId = "tenant-del";
        string userId = "user-del";
        int year = 2022;
        int month = 3;

        // Act - Typed overload path
        var typedBuilder = table.ComputedBothKeysEntitys.Delete(tenantId, userId, year, month);
        var typedRequest = typedBuilder.ToDeleteItemRequest();

        // Act - Standard overload path
        var manualPk = ComputedBothKeysEntity.Keys.BuildPk(tenantId, userId);
        var manualSk = ComputedBothKeysEntity.Keys.BuildSk(year, month);
        var standardBuilder = table.ComputedBothKeysEntitys.Delete(manualPk, manualSk);
        var standardRequest = standardBuilder.ToDeleteItemRequest();

        // Assert
        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }

    [Fact]
    public void ComputedPkOnly_Update_TypedOverloadMatchesStandard()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedPkOnlyTable(mockClient, "test-table");

        int year = 2024;
        int month = 11;
        int day = 30;

        // Act - Typed overload path
        var typedBuilder = table.ComputedPkOnlyEvents.Update(year, month, day);
        var typedRequest = typedBuilder.Set("SET #title = :t")
            .WithValue(":t", "test")
            .WithAttribute("#title", "title")
            .ToUpdateItemRequest();

        // Act - Standard overload path
        var manualPk = ComputedPkOnlyEvent.Keys.BuildPk(year, month, day);
        var standardBuilder = table.ComputedPkOnlyEvents.Update(manualPk);
        var standardRequest = standardBuilder.Set("SET #title = :t")
            .WithValue(":t", "test")
            .WithAttribute("#title", "title")
            .ToUpdateItemRequest();

        // Assert
        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
    }

    [Fact]
    public void ComputedBothKeys_Update_TypedOverloadMatchesStandard()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new TestComputedBothKeysTable(mockClient, "test-table");

        string tenantId = "tenant-upd";
        string userId = "user-upd";
        int year = 2025;
        int month = 2;

        // Act - Typed overload path
        var typedBuilder = table.ComputedBothKeysEntitys.Update(tenantId, userId, year, month);
        var typedRequest = typedBuilder.Set("SET #data = :d")
            .WithValue(":d", "updated")
            .WithAttribute("#data", "data")
            .ToUpdateItemRequest();

        // Act - Standard overload path
        var manualPk = ComputedBothKeysEntity.Keys.BuildPk(tenantId, userId);
        var manualSk = ComputedBothKeysEntity.Keys.BuildSk(year, month);
        var standardBuilder = table.ComputedBothKeysEntitys.Update(manualPk, manualSk);
        var standardRequest = standardBuilder.Set("SET #data = :d")
            .WithValue(":d", "updated")
            .WithAttribute("#data", "data")
            .ToUpdateItemRequest();

        // Assert
        typedRequest.Key["pk"].S.Should().Be(standardRequest.Key["pk"].S);
        typedRequest.Key["sk"].S.Should().Be(standardRequest.Key["sk"].S);
    }
}
