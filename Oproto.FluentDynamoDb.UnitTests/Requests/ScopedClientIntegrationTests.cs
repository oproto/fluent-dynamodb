using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Integration tests demonstrating STS scoped client scenarios.
/// These tests simulate service-layer patterns where tenant-specific STS clients
/// are provided for operations with tenant-scoped policies.
/// </summary>
public class ScopedClientIntegrationTests
{
    private readonly IAmazonDynamoDB _defaultClient = Substitute.For<IAmazonDynamoDB>();
    private readonly IAmazonDynamoDB _tenantScopedClient = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public async Task ServiceLayerPattern_GetOperation_ShouldUseScopedClient()
    {
        // Arrange - Simulate service layer generating scoped client
        var tenantId = "tenant123";
        var transactionId = "txn456";
        
        var expectedItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue($"{tenantId}#txn#{transactionId}"),
            ["sk"] = new AttributeValue("metadata"),
            ["amount"] = new AttributeValue { N = "100.50" },
            ["status"] = new AttributeValue("PENDING")
        };

        var expectedResponse = new GetItemResponse { Item = expectedItem };
        
        _tenantScopedClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act - Service layer uses scoped client for tenant-specific operation
        var response = await SimulateServiceLayerGetTransaction(tenantId, transactionId);

        // Assert
        response.Should().NotBeNull();
        response.Item.Should().ContainKey("pk");
        response.Item["pk"].S.Should().Be($"{tenantId}#txn#{transactionId}");
        
        // Verify scoped client was used, not default client
        await _tenantScopedClient.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(req => 
                req.TableName == "transactions" &&
                req.Key.ContainsKey("pk") &&
                req.Key["pk"].S == $"{tenantId}#txn#{transactionId}"),
            Arg.Any<CancellationToken>());
        
        await _defaultClient.DidNotReceive().GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceLayerPattern_QueryOperation_ShouldUseScopedClient()
    {
        // Arrange
        var tenantId = "tenant123";
        
        var expectedItems = new List<Dictionary<string, AttributeValue>>
        {
            new()
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#txn456"),
                ["sk"] = new AttributeValue("ledger789#line001"),
                ["amount"] = new AttributeValue { N = "50.25" }
            },
            new()
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#txn456"),
                ["sk"] = new AttributeValue("ledger789#line002"),
                ["amount"] = new AttributeValue { N = "25.75" }
            }
        };

        var expectedResponse = new QueryResponse 
        { 
            Items = expectedItems,
            Count = 2,
            ScannedCount = 2
        };
        
        _tenantScopedClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await SimulateServiceLayerQueryTransactions(tenantId);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => 
            item["pk"].S.Should().StartWith($"{tenantId}#txn#"));
        
        // Verify scoped client was used
        await _tenantScopedClient.Received(1).QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
        await _defaultClient.DidNotReceive().QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceLayerPattern_PutOperation_ShouldUseScopedClient()
    {
        // Arrange
        var tenantId = "tenant123";
        var transactionId = "txn789";
        
        var transactionItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue($"{tenantId}#txn#{transactionId}"),
            ["sk"] = new AttributeValue("metadata"),
            ["amount"] = new AttributeValue { N = "200.00" },
            ["status"] = new AttributeValue("ACTIVE"),
            ["createdAt"] = new AttributeValue("2024-01-15T10:30:00Z")
        };

        var expectedResponse = new PutItemResponse();
        
        _tenantScopedClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await SimulateServiceLayerCreateTransaction(tenantId, transactionId, transactionItem);

        // Assert
        response.Should().NotBeNull();
        
        // Verify scoped client was used with correct tenant constraint
        await _tenantScopedClient.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(req => 
                req.TableName == "transactions" &&
                req.Item.ContainsKey("pk") &&
                req.Item["pk"].S.StartsWith($"{tenantId}#txn#") &&
                req.ConditionExpression == "attribute_not_exists(pk)"),
            Arg.Any<CancellationToken>());
        
        await _defaultClient.DidNotReceive().PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceLayerPattern_UpdateOperation_ShouldUseScopedClient()
    {
        // Arrange
        var tenantId = "tenant123";
        var transactionId = "txn456";
        
        var expectedResponse = new UpdateItemResponse
        {
            Attributes = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#{transactionId}"),
                ["status"] = new AttributeValue("COMPLETED"),
                ["updatedAt"] = new AttributeValue("2024-01-15T11:00:00Z")
            }
        };
        
        _tenantScopedClient.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await SimulateServiceLayerUpdateTransactionStatus(tenantId, transactionId, "COMPLETED");

        // Assert
        response.Should().NotBeNull();
        response.Attributes.Should().ContainKey("status");
        response.Attributes["status"].S.Should().Be("COMPLETED");
        
        // Verify scoped client was used
        await _tenantScopedClient.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(req => 
                req.TableName == "transactions" &&
                req.Key.ContainsKey("pk") &&
                req.Key["pk"].S == $"{tenantId}#txn#{transactionId}" &&
                req.UpdateExpression.Contains("SET #status = :status") &&
                req.ConditionExpression == "attribute_exists(pk)"),
            Arg.Any<CancellationToken>());
        
        await _defaultClient.DidNotReceive().UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceLayerPattern_DeleteOperation_ShouldUseScopedClient()
    {
        // Arrange
        var tenantId = "tenant123";
        var transactionId = "txn456";
        
        var expectedResponse = new DeleteItemResponse
        {
            Attributes = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#{transactionId}"),
                ["status"] = new AttributeValue("DELETED")
            }
        };
        
        _tenantScopedClient.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await SimulateServiceLayerDeleteTransaction(tenantId, transactionId);

        // Assert
        response.Should().NotBeNull();
        response.Attributes.Should().ContainKey("pk");
        response.Attributes["pk"].S.Should().Be($"{tenantId}#txn#{transactionId}");
        
        // Verify scoped client was used
        await _tenantScopedClient.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(req => 
                req.TableName == "transactions" &&
                req.Key.ContainsKey("pk") &&
                req.Key["pk"].S == $"{tenantId}#txn#{transactionId}" &&
                req.ConditionExpression == "attribute_exists(pk)" &&
                req.ReturnValues == ReturnValue.ALL_OLD),
            Arg.Any<CancellationToken>());
        
        await _defaultClient.DidNotReceive().DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceLayerPattern_ScanOperation_ShouldUseScopedClient()
    {
        // Arrange
        var tenantId = "tenant123";
        
        var expectedItems = new List<Dictionary<string, AttributeValue>>
        {
            new()
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#txn001"),
                ["sk"] = new AttributeValue("metadata"),
                ["status"] = new AttributeValue("ACTIVE")
            },
            new()
            {
                ["pk"] = new AttributeValue($"{tenantId}#txn#txn002"),
                ["sk"] = new AttributeValue("metadata"),
                ["status"] = new AttributeValue("PENDING")
            }
        };

        var expectedResponse = new ScanResponse 
        { 
            Items = expectedItems,
            Count = 2,
            ScannedCount = 2
        };
        
        _tenantScopedClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await SimulateServiceLayerScanTransactions(tenantId);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => 
            item["pk"].S.Should().StartWith($"{tenantId}#txn#"));
        
        // Verify scoped client was used
        await _tenantScopedClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>());
        await _defaultClient.DidNotReceive().ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ServiceLayerPattern_ChainedOperations_ShouldPreserveClientThroughChain()
    {
        // Arrange
        var tenantId = "tenant123";
        var transactionId = "txn456";

        // Act - Build a complex query with scoped client
        var queryBuilder = new QueryRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .Where("pk = :pk")
            .WithValue(":pk", $"{tenantId}#txn#{transactionId}")
            .WithFilter("#status IN (:active, :pending)")
            .WithValue(":active", "ACTIVE")
            .WithValue(":pending", "PENDING")
            .WithAttribute("#status", "status")
            .WithProjection("#status, amount, createdAt")
            .Take(50)
            .OrderDescending()
            .WithClient(_tenantScopedClient); // Apply scoped client at the end

        // Assert - Verify all configuration is preserved
        var request = queryBuilder.ToQueryRequest();
        request.TableName.Should().Be("transactions");
        request.KeyConditionExpression.Should().Be("pk = :pk");
        request.FilterExpression.Should().Be("#status IN (:active, :pending)");
        request.ExpressionAttributeValues.Should().ContainKeys(":pk", ":active", ":pending");
        request.ExpressionAttributeValues[":pk"].S.Should().Be($"{tenantId}#txn#{transactionId}");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ProjectionExpression.Should().Be("#status, amount, createdAt");
        request.Limit.Should().Be(50);
        request.ScanIndexForward.Should().BeFalse();
    }

    // Simulate service layer methods that would use scoped clients

    private async Task<GetItemResponse> SimulateServiceLayerGetTransaction(string tenantId, string transactionId)
    {
        // This simulates how a service layer would use a scoped client
        return await new GetItemRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .WithKey("pk", $"{tenantId}#txn#{transactionId}")
            .WithKey("sk", "metadata")
            .WithClient(_tenantScopedClient) // Service provides tenant-scoped client
            .ExecuteAsync();
    }

    private async Task<QueryResponse> SimulateServiceLayerQueryTransactions(string tenantId)
    {
        return await new QueryRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .Where("pk = :pk")
            .WithValue(":pk", $"{tenantId}#txn#")
            .WithClient(_tenantScopedClient)
            .ExecuteAsync();
    }

    private async Task<PutItemResponse> SimulateServiceLayerCreateTransaction(
        string tenantId, string transactionId, Dictionary<string, AttributeValue> item)
    {
        return await new PutItemRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .WithItem(item)
            .Where("attribute_not_exists(pk)")
            .WithClient(_tenantScopedClient)
            .ExecuteAsync();
    }

    private async Task<UpdateItemResponse> SimulateServiceLayerUpdateTransactionStatus(
        string tenantId, string transactionId, string newStatus)
    {
        return await new UpdateItemRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .WithKey("pk", $"{tenantId}#txn#{transactionId}")
            .WithKey("sk", "metadata")
            .Set("SET #status = :status, #updatedAt = :updatedAt")
            .Where("attribute_exists(pk)")
            .WithAttribute("#status", "status")
            .WithAttribute("#updatedAt", "updatedAt")
            .WithValue(":status", newStatus)
            .WithValue(":updatedAt", DateTime.UtcNow.ToString("O"))
            .WithClient(_tenantScopedClient)
            .ExecuteAsync();
    }

    private async Task<DeleteItemResponse> SimulateServiceLayerDeleteTransaction(string tenantId, string transactionId)
    {
        return await new DeleteItemRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .WithKey("pk", $"{tenantId}#txn#{transactionId}")
            .WithKey("sk", "metadata")
            .Where("attribute_exists(pk)")
            .ReturnAllOldValues()
            .WithClient(_tenantScopedClient)
            .ExecuteAsync();
    }

    private async Task<ScanResponse> SimulateServiceLayerScanTransactions(string tenantId)
    {
        return await new ScanRequestBuilder(_defaultClient)
            .ForTable("transactions")
            .WithFilter("begins_with(pk, :tenantPrefix) AND sk = :sk")
            .WithValue(":tenantPrefix", $"{tenantId}#txn#")
            .WithValue(":sk", "metadata")
            .WithProjection("pk, #status, amount")
            .WithAttribute("#status", "status")
            .Take(100)
            .WithClient(_tenantScopedClient)
            .ExecuteAsync();
    }
}