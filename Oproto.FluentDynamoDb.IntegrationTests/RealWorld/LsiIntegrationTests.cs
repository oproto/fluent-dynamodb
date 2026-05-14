using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for Local Secondary Index (LSI) support.
/// Tests verify that LSI metadata is correctly generated and that
/// queries via LSI work correctly with DynamoDB Local.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "LSI")]
public class LsiIntegrationTests : IntegrationTestBase
{
    public LsiIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Table wrapper for testing LSI queries.
    /// </summary>
    private class LsiTestTable : GenericTable
    {
        public DynamoDbIndex OrderDateIndex { get; }
        
        public LsiTestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            OrderDateIndex = new DynamoDbIndex(this, "OrderDateIndex");
        }
        
        public async Task PutAsync(LsiTestEntity entity)
        {
            var item = LsiTestEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    /// <summary>
    /// Creates a DynamoDB table with a Local Secondary Index for LsiTestEntity.
    /// </summary>
    private async Task CreateTableWithLsiAsync()
    {
        var metadata = LsiTestEntity.GetEntityMetadata();
        
        // Find partition key property
        var partitionKeyProp = metadata.Properties.FirstOrDefault(p => p.IsPartitionKey);
        if (partitionKeyProp == null)
        {
            throw new InvalidOperationException("Entity does not have a partition key property");
        }
        
        // Find sort key property
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        if (sortKeyProp == null)
        {
            throw new InvalidOperationException("Entity does not have a sort key property");
        }
        
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition
            {
                AttributeName = partitionKeyProp.AttributeName,
                AttributeType = ScalarAttributeType.S
            },
            new AttributeDefinition
            {
                AttributeName = sortKeyProp.AttributeName,
                AttributeType = ScalarAttributeType.S
            },
            // LSI sort key attribute
            new AttributeDefinition
            {
                AttributeName = "order_date",
                AttributeType = ScalarAttributeType.S
            }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement
            {
                AttributeName = partitionKeyProp.AttributeName,
                KeyType = KeyType.HASH
            },
            new KeySchemaElement
            {
                AttributeName = sortKeyProp.AttributeName,
                KeyType = KeyType.RANGE
            }
        };
        
        // LSI key schema - shares partition key with base table
        var lsiKeySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement
            {
                AttributeName = partitionKeyProp.AttributeName,
                KeyType = KeyType.HASH
            },
            new KeySchemaElement
            {
                AttributeName = "order_date",
                KeyType = KeyType.RANGE
            }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            LocalSecondaryIndexes = new List<LocalSecondaryIndex>
            {
                new LocalSecondaryIndex
                {
                    IndexName = "OrderDateIndex",
                    KeySchema = lsiKeySchema,
                    Projection = new Projection
                    {
                        ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL
                    }
                }
            }
        };
        
        await DynamoDb.CreateTableAsync(request);
        
        Console.WriteLine($"[Setup] Created table with LSI: {TableName} (LSI: OrderDateIndex)");
        
        // Wait for table to be active
        await WaitForTableActiveAsync(TableName);
    }
    
    /// <summary>
    /// Tests that a table with LSI can be created in DynamoDB Local.
    /// </summary>
    [Fact]
    public async Task CreateTableWithLsi_Succeeds()
    {
        // Act
        await CreateTableWithLsiAsync();
        
        // Assert - Verify table exists and has LSI
        var describeResponse = await DynamoDb.DescribeTableAsync(TableName);
        
        describeResponse.Table.Should().NotBeNull();
        describeResponse.Table.TableStatus.Should().Be(TableStatus.ACTIVE);
        describeResponse.Table.LocalSecondaryIndexes.Should().HaveCount(1);
        
        var lsi = describeResponse.Table.LocalSecondaryIndexes[0];
        lsi.IndexName.Should().Be("OrderDateIndex");
        lsi.KeySchema.Should().HaveCount(2);
        lsi.KeySchema[0].AttributeName.Should().Be("pk");
        lsi.KeySchema[0].KeyType.Should().Be(KeyType.HASH);
        lsi.KeySchema[1].AttributeName.Should().Be("order_date");
        lsi.KeySchema[1].KeyType.Should().Be(KeyType.RANGE);
    }
    
    /// <summary>
    /// Tests that items can be queried via LSI.
    /// </summary>
    [Fact]
    public async Task QueryViaLsi_ReturnsItemsSortedByLsiSortKey()
    {
        // Arrange
        await CreateTableWithLsiAsync();
        var table = new LsiTestTable(DynamoDb, TableName);
        
        // Create test data - multiple orders for the same customer
        var customerId = "customer-001";
        var orders = new[]
        {
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-003",
                OrderDate = "2024-01-15",
                Total = 150.00m,
                Status = "Shipped",
                ProductName = "Widget C",
                Quantity = 3
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-001",
                OrderDate = "2024-01-01",
                Total = 100.00m,
                Status = "Delivered",
                ProductName = "Widget A",
                Quantity = 1
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-002",
                OrderDate = "2024-01-10",
                Total = 200.00m,
                Status = "Processing",
                ProductName = "Widget B",
                Quantity = 2
            }
        };
        
        foreach (var order in orders)
        {
            await table.PutAsync(order);
        }
        
        // Act - Query via LSI (sorted by order_date)
        var response = await table.OrderDateIndex.Query<LsiTestEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", customerId)
            .ToDynamoDbResponseAsync();
        
        // Assert - Items should be sorted by order_date (ascending)
        response.Items.Should().HaveCount(3);
        
        var results = response.Items
            .Select(item => LsiTestEntity.FromDynamoDb<LsiTestEntity>(item))
            .ToList();
        
        results[0].OrderDate.Should().Be("2024-01-01");
        results[0].OrderId.Should().Be("order-001");
        
        results[1].OrderDate.Should().Be("2024-01-10");
        results[1].OrderId.Should().Be("order-002");
        
        results[2].OrderDate.Should().Be("2024-01-15");
        results[2].OrderId.Should().Be("order-003");
    }
    
    /// <summary>
    /// Tests that LSI query with sort key condition works correctly.
    /// </summary>
    [Fact]
    public async Task QueryViaLsi_WithSortKeyCondition_FiltersCorrectly()
    {
        // Arrange
        await CreateTableWithLsiAsync();
        var table = new LsiTestTable(DynamoDb, TableName);
        
        var customerId = "customer-002";
        var orders = new[]
        {
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-a",
                OrderDate = "2024-01-05",
                Total = 50.00m,
                Status = "Delivered"
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-b",
                OrderDate = "2024-01-15",
                Total = 75.00m,
                Status = "Shipped"
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-c",
                OrderDate = "2024-01-25",
                Total = 125.00m,
                Status = "Processing"
            }
        };
        
        foreach (var order in orders)
        {
            await table.PutAsync(order);
        }
        
        // Act - Query via LSI with sort key condition (orders after Jan 10)
        var response = await table.OrderDateIndex.Query<LsiTestEntity>()
            .Where("pk = :pk AND order_date > :date")
            .WithValue(":pk", customerId)
            .WithValue(":date", "2024-01-10")
            .ToDynamoDbResponseAsync();
        
        // Assert - Only orders after Jan 10 should be returned
        response.Items.Should().HaveCount(2);
        
        var results = response.Items
            .Select(item => LsiTestEntity.FromDynamoDb<LsiTestEntity>(item))
            .ToList();
        
        results.Should().AllSatisfy(r => 
            string.Compare(r.OrderDate, "2024-01-10", StringComparison.Ordinal).Should().BeGreaterThan(0));
        results.Select(r => r.OrderId).Should().BeEquivalentTo(new[] { "order-b", "order-c" });
    }
    
    /// <summary>
    /// Tests that LSI metadata is correctly generated by the source generator.
    /// </summary>
    [Fact]
    public void LsiMetadata_IsCorrectlyGenerated()
    {
        // Act
        var metadata = LsiTestEntity.GetEntityMetadata();
        
        // Assert - Verify entity has indexes
        metadata.Indexes.Should().NotBeNull();
        metadata.Indexes.Should().HaveCount(1);
        
        var lsiMetadata = metadata.Indexes[0];
        lsiMetadata.IndexName.Should().Be("OrderDateIndex");
        lsiMetadata.IndexType.Should().Be(IndexType.LocalSecondaryIndex);
        
        // LSI shares partition key with base table
        lsiMetadata.PartitionKeyProperty.Should().Be("CustomerId");
        lsiMetadata.PartitionKeyAttributeName.Should().Be("pk");
        
        // LSI has its own sort key
        lsiMetadata.SortKeyProperty.Should().Be("OrderDate");
        lsiMetadata.SortKeyAttributeName.Should().Be("order_date");
    }
    
    /// <summary>
    /// Tests that LSI query in descending order works correctly.
    /// </summary>
    [Fact]
    public async Task QueryViaLsi_Descending_ReturnsItemsInReverseOrder()
    {
        // Arrange
        await CreateTableWithLsiAsync();
        var table = new LsiTestTable(DynamoDb, TableName);
        
        var customerId = "customer-003";
        var orders = new[]
        {
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-x",
                OrderDate = "2024-02-01",
                Total = 100.00m
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-y",
                OrderDate = "2024-02-15",
                Total = 200.00m
            },
            new LsiTestEntity
            {
                CustomerId = customerId,
                OrderId = "order-z",
                OrderDate = "2024-02-28",
                Total = 300.00m
            }
        };
        
        foreach (var order in orders)
        {
            await table.PutAsync(order);
        }
        
        // Act - Query via LSI in descending order
        var response = await table.OrderDateIndex.Query<LsiTestEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", customerId)
            .OrderDescending()
            .ToDynamoDbResponseAsync();
        
        // Assert - Items should be sorted by order_date (descending)
        response.Items.Should().HaveCount(3);
        
        var results = response.Items
            .Select(item => LsiTestEntity.FromDynamoDb<LsiTestEntity>(item))
            .ToList();
        
        results[0].OrderDate.Should().Be("2024-02-28");
        results[1].OrderDate.Should().Be("2024-02-15");
        results[2].OrderDate.Should().Be("2024-02-01");
    }
}
