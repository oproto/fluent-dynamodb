using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.UnitTests.Provisioning;

/// <summary>
/// Unit tests for TableCreator.CreateAsync method.
/// Tests cover default options behavior, TTL enablement, wait-for-active, and error handling.
/// </summary>
public class TableCreatorTests
{
    private readonly TableCreator _tableCreator = new();

    #region Default Options Behavior Tests

    [Fact]
    public async Task CreateAsync_WithDefaultOptions_UsesPAYPERREQUESTBillingMode()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.ACTIVE));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));

        // Act
        await _tableCreator.CreateAsync(mockClient, "TestTable", metadata);

        // Assert
        await mockClient.Received(1).CreateTableAsync(
            Arg.Is<CreateTableRequest>(r => r.BillingMode == BillingMode.PAY_PER_REQUEST),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithDefaultOptions_WaitsForTableToBeActive()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.CREATING));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata);

        // Assert
        await mockClient.Received().DescribeTableAsync(
            Arg.Is<DescribeTableRequest>(r => r.TableName == "TestTable"),
            Arg.Any<CancellationToken>());
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCorrectTableCreationResult()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.ACTIVE, "arn:aws:dynamodb:us-east-1:123456789:table/TestTable"));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata);

        // Assert
        result.TableName.Should().Be("TestTable");
        result.TableArn.Should().Be("arn:aws:dynamodb:us-east-1:123456789:table/TestTable");
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        result.TtlEnabled.Should().BeFalse();
    }

    #endregion

    #region TTL Enablement Tests

    [Fact]
    public async Task CreateAsync_WithEnableTtlAndTtlAttribute_EnablesTtl()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        metadata.TtlAttributeName = "expiresAt";
        
        var options = new TableCreationOptions { EnableTtl = true };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.ACTIVE));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));
        
        mockClient.UpdateTimeToLiveAsync(Arg.Any<UpdateTimeToLiveRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateTimeToLiveResponse());

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);

        // Assert
        await mockClient.Received(1).UpdateTimeToLiveAsync(
            Arg.Is<UpdateTimeToLiveRequest>(r => 
                r.TableName == "TestTable" &&
                r.TimeToLiveSpecification.Enabled == true &&
                r.TimeToLiveSpecification.AttributeName == "expiresAt"),
            Arg.Any<CancellationToken>());
        result.TtlEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithEnableTtlButNoTtlAttribute_DoesNotEnableTtl()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        // No TtlAttributeName set
        
        var options = new TableCreationOptions { EnableTtl = true };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.ACTIVE));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);

        // Assert
        await mockClient.DidNotReceive().UpdateTimeToLiveAsync(
            Arg.Any<UpdateTimeToLiveRequest>(),
            Arg.Any<CancellationToken>());
        result.TtlEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithTtlAttributeButEnableTtlFalse_DoesNotEnableTtl()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        metadata.TtlAttributeName = "expiresAt";
        
        var options = new TableCreationOptions { EnableTtl = false };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.ACTIVE));
        
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.ACTIVE));

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);

        // Assert
        await mockClient.DidNotReceive().UpdateTimeToLiveAsync(
            Arg.Any<UpdateTimeToLiveRequest>(),
            Arg.Any<CancellationToken>());
        result.TtlEnabled.Should().BeFalse();
    }

    #endregion

    #region Wait-for-Active Tests

    [Fact]
    public async Task CreateAsync_WithWaitForActiveFalse_ReturnsImmediately()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        var options = new TableCreationOptions { WaitForActive = false };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.CREATING));

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);

        // Assert
        // Should not call DescribeTable when WaitForActive is false
        await mockClient.DidNotReceive().DescribeTableAsync(
            Arg.Any<DescribeTableRequest>(),
            Arg.Any<CancellationToken>());
        result.TableStatus.Should().Be(TableStatus.CREATING);
    }

    [Fact]
    public async Task CreateAsync_WhenTableBecomesActiveAfterPolling_ReturnsActiveStatus()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        var options = new TableCreationOptions 
        { 
            WaitForActive = true,
            PollingInterval = TimeSpan.FromMilliseconds(10)
        };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.CREATING));
        
        // First call returns CREATING, second returns ACTIVE
        var callCount = 0;
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return CreateDescribeTableResponse("TestTable", 
                    callCount == 1 ? TableStatus.CREATING : TableStatus.ACTIVE);
            });

        // Act
        var result = await _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);

        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CreateAsync_WhenTimeoutExceeded_ThrowsTimeoutException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();
        var options = new TableCreationOptions 
        { 
            WaitForActive = true,
            WaitTimeout = TimeSpan.FromMilliseconds(50),
            PollingInterval = TimeSpan.FromMilliseconds(10)
        };
        
        mockClient.CreateTableAsync(Arg.Any<CreateTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateTableResponse("TestTable", TableStatus.CREATING));
        
        // Always return CREATING to trigger timeout
        mockClient.DescribeTableAsync(Arg.Any<DescribeTableRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateDescribeTableResponse("TestTable", TableStatus.CREATING));

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, "TestTable", metadata, options);
        
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*TestTable*did not become ACTIVE*");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void CreateAsync_WithNullTableName_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, null!, metadata);
        
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Table name cannot be null or empty*");
    }

    [Fact]
    public void CreateAsync_WithEmptyTableName_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = CreateBasicMetadata();

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, "", metadata);
        
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Table name cannot be null or empty*");
    }

    [Fact]
    public void CreateAsync_WithMissingPartitionKey_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            PartitionKeyAttributeName = null!,
            PartitionKeyAttributeType = "S"
        };

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, "TestTable", metadata);
        
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*partition key*");
    }

    [Fact]
    public void CreateAsync_WithInvalidPartitionKeyType_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "INVALID"
        };

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, "TestTable", metadata);
        
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid partition key attribute type*");
    }

    [Fact]
    public void CreateAsync_WithInvalidSortKeyType_ThrowsArgumentException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = "sk",
            SortKeyAttributeType = "INVALID"
        };

        // Act & Assert
        var act = () => _tableCreator.CreateAsync(mockClient, "TestTable", metadata);
        
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid sort key attribute type*");
    }

    #endregion

    #region Helper Methods

    private static EntityMetadata CreateBasicMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            Indexes = Array.Empty<IndexMetadata>()
        };
    }

    private static Task<CreateTableResponse> CreateTableResponse(
        string tableName, 
        TableStatus status, 
        string? tableArn = null)
    {
        return Task.FromResult(new CreateTableResponse
        {
            TableDescription = new TableDescription
            {
                TableName = tableName,
                TableArn = tableArn ?? $"arn:aws:dynamodb:us-east-1:123456789:table/{tableName}",
                TableStatus = status
            }
        });
    }

    private static Task<DescribeTableResponse> CreateDescribeTableResponse(
        string tableName, 
        TableStatus status)
    {
        return Task.FromResult(new DescribeTableResponse
        {
            Table = new TableDescription
            {
                TableName = tableName,
                TableStatus = status
            }
        });
    }

    #endregion
}
