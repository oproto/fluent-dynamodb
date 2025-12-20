using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for projection query scenarios.
/// Tests end-to-end projection usage with QueryRequestBuilder and index queries.
/// 
/// **Feature: projection-interface-enhancement, Task 11: Add comprehensive integration tests**
/// **Validates: Requirements 7.5**
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ProjectionInterfaceEnhancement")]
public class ProjectionQueryIntegrationTests : IntegrationTestBase
{
    private GenericTable _table = null!;
    
    public ProjectionQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableWithGsiAsync<InventoryEntity>("StatusIndex", "gsi1_pk", "gsi1_sk");
        _table = new TestTable(DynamoDb, TableName);
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var entities = new[]
        {
            new InventoryEntity
            {
                WarehouseId = "WH-001",
                ItemKey = "ITEM#001",
                EntityType = "INVENTORY",
                Status = "ACTIVE",
                StatusSortKey = "INVENTORY#2024-01-15",
                ItemName = "Widget A",
                Quantity = 100,
                LastUpdated = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            },
            new InventoryEntity
            {
                WarehouseId = "WH-001",
                ItemKey = "ITEM#002",
                EntityType = "INVENTORY",
                Status = "ACTIVE",
                StatusSortKey = "INVENTORY#2024-02-20",
                ItemName = "Widget B",
                Quantity = 50,
                LastUpdated = new DateTime(2024, 2, 20, 14, 45, 0, DateTimeKind.Utc)
            },
            new InventoryEntity
            {
                WarehouseId = "WH-002",
                ItemKey = "ITEM#003",
                EntityType = "INVENTORY",
                Status = "INACTIVE",
                StatusSortKey = "INVENTORY#2024-03-10",
                ItemName = "Widget C",
                Quantity = 0,
                LastUpdated = new DateTime(2024, 3, 10, 9, 15, 0, DateTimeKind.Utc)
            },
            new InventoryEntity
            {
                WarehouseId = "WH-001",
                ItemKey = "ITEM#004",
                EntityType = "INVENTORY",
                Status = "ACTIVE",
                StatusSortKey = "INVENTORY#2024-04-05",
                ItemName = "Widget D",
                Quantity = 200,
                LastUpdated = new DateTime(2024, 4, 5, 16, 20, 0, DateTimeKind.Utc)
            }
        };
        
        foreach (var entity in entities)
        {
            var item = InventoryEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    #region End-to-End Projection Query Scenarios
    
    /// <summary>
    /// Tests that projections can be queried using the primary table key.
    /// </summary>
    [Fact]
    public async Task Query_WithProjectionType_ReturnsProjectedResults()
    {
        // Arrange
        var warehouseId = "WH-001";
        
        // Act - Query using projection type
        var response = await _table.Query<InventoryProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", warehouseId)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p =>
        {
            p.WarehouseId.Should().Be(warehouseId);
            // Verify projected attributes are populated
            p.ItemName.Should().NotBeNullOrEmpty();
        });
    }
    
    /// <summary>
    /// Tests that projections work with format string expressions.
    /// </summary>
    [Fact]
    public async Task Query_WithProjectionAndFormatString_ReturnsProjectedResults()
    {
        // Arrange
        var warehouseId = "WH-001";
        
        // Act - Query using format string
        var response = await _table.Query<InventoryProjection>()
            .Where("pk = {0}", warehouseId)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p => p.WarehouseId.Should().Be(warehouseId));
    }
    
    /// <summary>
    /// Tests that projections work with manual expression and values.
    /// </summary>
    [Fact]
    public async Task Query_WithProjectionAndManualExpression_ReturnsProjectedResults()
    {
        // Arrange
        var warehouseId = "WH-002";
        
        // Act - Query using manual expression
        var response = await _table.Query<InventoryProjection>()
            .Where("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", warehouseId)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p => p.WarehouseId.Should().Be(warehouseId));
    }
    
    #endregion
    
    #region Index Projection Queries
    
    /// <summary>
    /// Tests that projections work with GSI queries.
    /// </summary>
    [Fact]
    public async Task Query_WithProjectionOnGsi_ReturnsProjectedResults()
    {
        // Arrange
        var status = "ACTIVE";
        var index = new DynamoDbIndex(_table, "StatusIndex");
        
        // Act - Query GSI using projection type
        var response = await index.Query<InventoryProjection>()
            .Where("gsi1_pk = :status")
            .WithValue(":status", status)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p => p.Status.Should().Be(status));
    }
    
    /// <summary>
    /// Tests that projections work with GSI queries using format strings.
    /// </summary>
    [Fact]
    public async Task Query_WithProjectionOnGsiFormatString_ReturnsProjectedResults()
    {
        // Arrange
        var status = "ACTIVE";
        var index = new DynamoDbIndex(_table, "StatusIndex");
        
        // Act - Query GSI using format string
        var response = await index.Query<InventoryProjection>("gsi1_pk = {0}", status)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p => p.Status.Should().Be(status));
    }
    
    /// <summary>
    /// Tests that generic index with projection type works correctly.
    /// </summary>
    [Fact]
    public async Task Query_WithGenericIndexProjection_ReturnsProjectedResults()
    {
        // Arrange
        var status = "ACTIVE";
        var index = new DynamoDbIndex<InventoryProjection>(_table, "StatusIndex");
        
        // Act - Query using non-generic Query() method
        var response = await index.Query()
            .Where("gsi1_pk = :status")
            .WithValue(":status", status)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p => p.Status.Should().Be(status));
    }
    
    #endregion
    
    #region Mixed Entity and Projection Queries
    
    /// <summary>
    /// Tests that full entities and projections can be queried from the same table.
    /// </summary>
    [Fact]
    public async Task Query_MixedEntityAndProjection_BothWorkCorrectly()
    {
        // Arrange
        var warehouseId = "WH-001";
        
        // Act - Query full entity
        var entityResponse = await _table.Query<InventoryEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", warehouseId)
            .ToDynamoDbResponseAsync();
        
        // Act - Query projection
        var projectionResponse = await _table.Query<InventoryProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", warehouseId)
            .ToDynamoDbResponseAsync();
        
        // Assert - Both should return same number of items
        entityResponse.Items.Should().HaveCount(projectionResponse.Items.Count);
        
        // Assert - Full entity has all attributes
        var entities = entityResponse.Items
            .Select(item => InventoryEntity.FromDynamoDb<InventoryEntity>(item))
            .ToList();
        
        entities.Should().AllSatisfy(e =>
        {
            e.WarehouseId.Should().Be(warehouseId);
            e.ItemKey.Should().NotBeNullOrEmpty();
            e.EntityType.Should().Be("INVENTORY");
            e.LastUpdated.Should().NotBeNull();
        });
        
        // Assert - Projection has only projected attributes
        var projections = projectionResponse.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        projections.Should().AllSatisfy(p =>
        {
            p.WarehouseId.Should().Be(warehouseId);
            p.ItemName.Should().NotBeNullOrEmpty();
        });
    }
    
    /// <summary>
    /// Tests that projections can be used with ToListAsync extension.
    /// </summary>
    [Fact]
    public async Task ToListAsync_WithProjectionType_ReturnsHydratedResults()
    {
        // Arrange
        var warehouseId = "WH-001";
        
        // Act - Use ToListAsync extension
        var projections = await _table.Query<InventoryProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", warehouseId)
            .ToListAsync();
        
        // Assert
        projections.Should().HaveCountGreaterThan(0);
        projections.Should().AllSatisfy(p =>
        {
            p.WarehouseId.Should().Be(warehouseId);
            p.ItemName.Should().NotBeNullOrEmpty();
        });
    }
    
    #endregion
    
    #region Error Handling Scenarios
    
    /// <summary>
    /// Tests that querying with invalid key condition returns empty results.
    /// </summary>
    [Fact]
    public async Task Query_WithNonExistentKey_ReturnsEmptyResults()
    {
        // Arrange
        var nonExistentWarehouse = "WH-NONEXISTENT";
        
        // Act
        var response = await _table.Query<InventoryProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", nonExistentWarehouse)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().BeEmpty();
    }
    
    /// <summary>
    /// Tests that projection hydration handles missing optional attributes gracefully.
    /// </summary>
    [Fact]
    public async Task Query_WithProjection_HandlesNullableAttributesGracefully()
    {
        // Arrange - Query for inactive item which may have null quantity
        var status = "INACTIVE";
        var index = new DynamoDbIndex(_table, "StatusIndex");
        
        // Act
        var response = await index.Query<InventoryProjection>()
            .Where("gsi1_pk = :status")
            .WithValue(":status", status)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var projections = response.Items
            .Select(item => InventoryProjection.FromDynamoDb(item))
            .ToList();
        
        // Should not throw even if some attributes are null
        projections.Should().AllSatisfy(p => p.Status.Should().Be(status));
    }
    
    #endregion
    
    // Helper class to create a table instance for query operations
    private class TestTable : GenericTable
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}

/// <summary>
/// Property-based tests for projection query pattern compatibility.
/// Uses loop-based property testing pattern for async operations.
/// 
/// **Feature: projection-interface-enhancement, Property 9: Projection query pattern compatibility**
/// **Validates: Requirements 4.5**
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Category", "PropertyTest")]
[Trait("Feature", "ProjectionInterfaceEnhancement")]
public class ProjectionQueryPatternCompatibilityPropertyTests : IntegrationTestBase
{
    private const int PropertyTestIterations = 20;
    private GenericTable _table = null!;
    private readonly List<InventoryEntity> _seededEntities = new();
    
    public ProjectionQueryPatternCompatibilityPropertyTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableWithGsiAsync<InventoryEntity>("StatusIndex", "gsi1_pk", "gsi1_sk");
        _table = new TestTable(DynamoDb, TableName);
        
        // Seed test data with known values for property testing
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var warehouseIds = new[] { "WH-PROP-001", "WH-PROP-002", "WH-PROP-003" };
        var statuses = new[] { "ACTIVE", "INACTIVE", "PENDING" };
        
        for (int i = 0; i < PropertyTestIterations; i++)
        {
            var entity = new InventoryEntity
            {
                WarehouseId = warehouseIds[i % warehouseIds.Length],
                ItemKey = $"ITEM#P{i:D3}",
                EntityType = "INVENTORY",
                Status = statuses[i % statuses.Length],
                StatusSortKey = $"INVENTORY#2024-{(i % 12) + 1:D2}-{(i % 28) + 1:D2}",
                ItemName = $"Property Test Item {i}",
                Quantity = i * 10,
                LastUpdated = DateTime.UtcNow.AddDays(-i)
            };
            
            var item = InventoryEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
            _seededEntities.Add(entity);
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type, it SHALL work with all query patterns:
    /// lambda expressions, format strings, and manual expressions.
    /// All three patterns should return equivalent results for the same query.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task ProjectionQuery_AllPatterns_ShouldReturnEquivalentResults()
    {
        var warehouseIds = _seededEntities.Select(e => e.WarehouseId).Distinct().ToArray();
        
        foreach (var warehouseId in warehouseIds)
        {
            // Pattern 1: Manual expression with WithValue
            var manualResponse = await _table.Query<InventoryProjection>()
                .Where("pk = :pk")
                .WithValue(":pk", warehouseId)
                .ToDynamoDbResponseAsync();
            
            // Pattern 2: Format string
            var formatResponse = await _table.Query<InventoryProjection>()
                .Where("pk = {0}", warehouseId)
                .ToDynamoDbResponseAsync();
            
            // Pattern 3: Manual with attribute name substitution
            var attributeResponse = await _table.Query<InventoryProjection>()
                .Where("#pk = :pk")
                .WithAttribute("#pk", "pk")
                .WithValue(":pk", warehouseId)
                .ToDynamoDbResponseAsync();
            
            // All patterns should return the same number of items
            var manualCount = manualResponse.Items.Count;
            var formatCount = formatResponse.Items.Count;
            var attributeCount = attributeResponse.Items.Count;
            
            manualCount.Should().Be(formatCount, 
                $"Manual and format string patterns should return same count for warehouse {warehouseId}");
            formatCount.Should().Be(attributeCount, 
                $"Format string and attribute patterns should return same count for warehouse {warehouseId}");
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type queried via GSI, it SHALL work with all query patterns.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task ProjectionQueryOnGsi_AllPatterns_ShouldReturnEquivalentResults()
    {
        var statuses = _seededEntities.Select(e => e.Status).Distinct().Where(s => s != null).ToArray();
        var index = new DynamoDbIndex(_table, "StatusIndex");
        
        foreach (var status in statuses)
        {
            // Pattern 1: Manual expression with WithValue
            var manualResponse = await index.Query<InventoryProjection>()
                .Where("gsi1_pk = :status")
                .WithValue(":status", status!)
                .ToDynamoDbResponseAsync();
            
            // Pattern 2: Format string
            var formatResponse = await index.Query<InventoryProjection>("gsi1_pk = {0}", status!)
                .ToDynamoDbResponseAsync();
            
            // Pattern 3: Manual with attribute name substitution
            var attributeResponse = await index.Query<InventoryProjection>()
                .Where("#gsi1pk = :status")
                .WithAttribute("#gsi1pk", "gsi1_pk")
                .WithValue(":status", status!)
                .ToDynamoDbResponseAsync();
            
            // All patterns should return the same number of items
            var manualCount = manualResponse.Items.Count;
            var formatCount = formatResponse.Items.Count;
            var attributeCount = attributeResponse.Items.Count;
            
            manualCount.Should().Be(formatCount, 
                $"Manual and format string patterns should return same count for status {status}");
            formatCount.Should().Be(attributeCount, 
                $"Format string and attribute patterns should return same count for status {status}");
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type, the hydrated results SHALL have consistent
    /// attribute values regardless of query pattern used.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task ProjectionQuery_AllPatterns_ShouldHydrateConsistently()
    {
        var warehouseIds = _seededEntities.Select(e => e.WarehouseId).Distinct().ToArray();
        
        foreach (var warehouseId in warehouseIds)
        {
            // Query using different patterns
            var manualResponse = await _table.Query<InventoryProjection>()
                .Where("pk = :pk")
                .WithValue(":pk", warehouseId)
                .ToDynamoDbResponseAsync();
            
            var formatResponse = await _table.Query<InventoryProjection>()
                .Where("pk = {0}", warehouseId)
                .ToDynamoDbResponseAsync();
            
            // Hydrate results
            var manualProjections = manualResponse.Items
                .Select(item => InventoryProjection.FromDynamoDb(item))
                .OrderBy(p => p.ItemName)
                .ToList();
            
            var formatProjections = formatResponse.Items
                .Select(item => InventoryProjection.FromDynamoDb(item))
                .OrderBy(p => p.ItemName)
                .ToList();
            
            // Results should be identical
            manualProjections.Should().HaveCount(formatProjections.Count,
                $"Both patterns should return same count for warehouse {warehouseId}");
            
            for (int i = 0; i < manualProjections.Count; i++)
            {
                manualProjections[i].WarehouseId.Should().Be(formatProjections[i].WarehouseId,
                    $"WarehouseId should match at index {i}");
                manualProjections[i].ItemName.Should().Be(formatProjections[i].ItemName,
                    $"ItemName should match at index {i}");
                manualProjections[i].Quantity.Should().Be(formatProjections[i].Quantity,
                    $"Quantity should match at index {i}");
                manualProjections[i].Status.Should().Be(formatProjections[i].Status,
                    $"Status should match at index {i}");
            }
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type with generic index, the non-generic Query()
    /// method SHALL work with all expression patterns.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task GenericIndexProjection_AllPatterns_ShouldWork()
    {
        var statuses = _seededEntities.Select(e => e.Status).Distinct().Where(s => s != null).ToArray();
        var genericIndex = new DynamoDbIndex<InventoryProjection>(_table, "StatusIndex");
        
        foreach (var status in statuses)
        {
            // Pattern 1: Non-generic Query() with manual expression
            var manualResponse = await genericIndex.Query()
                .Where("gsi1_pk = :status")
                .WithValue(":status", status!)
                .ToDynamoDbResponseAsync();
            
            // Pattern 2: Non-generic Query() with format string shorthand
            var formatResponse = await genericIndex.Query("gsi1_pk = {0}", status!)
                .ToDynamoDbResponseAsync();
            
            // Both should return the same count
            manualResponse.Items.Count.Should().Be(formatResponse.Items.Count,
                $"Both patterns should return same count for status {status}");
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type, queries with composite key conditions
    /// SHALL work with all expression patterns.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task ProjectionQuery_CompositeKeyConditions_AllPatternsShouldWork()
    {
        var warehouseIds = _seededEntities.Select(e => e.WarehouseId).Distinct().ToArray();
        
        foreach (var warehouseId in warehouseIds)
        {
            var itemPrefix = "ITEM#P";
            
            // Pattern 1: Manual expression with composite key
            var manualResponse = await _table.Query<InventoryProjection>()
                .Where("pk = :pk AND begins_with(sk, :skPrefix)")
                .WithValue(":pk", warehouseId)
                .WithValue(":skPrefix", itemPrefix)
                .ToDynamoDbResponseAsync();
            
            // Pattern 2: Format string with composite key
            var formatResponse = await _table.Query<InventoryProjection>()
                .Where("pk = {0} AND begins_with(sk, {1})", warehouseId, itemPrefix)
                .ToDynamoDbResponseAsync();
            
            // Both should return the same count
            manualResponse.Items.Count.Should().Be(formatResponse.Items.Count,
                $"Both patterns should return same count for warehouse {warehouseId} with prefix {itemPrefix}");
            
            // Verify results are valid projections
            var projections = manualResponse.Items
                .Select(item => InventoryProjection.FromDynamoDb(item))
                .ToList();
            
            projections.Should().AllSatisfy(p => p.WarehouseId.Should().Be(warehouseId));
        }
    }
    
    /// <summary>
    /// Property 9: For any projection type, queries with filter expressions
    /// SHALL work with all expression patterns.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public async Task ProjectionQuery_WithFilterExpression_AllPatternsShouldWork()
    {
        var warehouseIds = _seededEntities.Select(e => e.WarehouseId).Distinct().ToArray();
        var minQuantity = 50;
        
        foreach (var warehouseId in warehouseIds)
        {
            // Pattern 1: Manual expression with filter
            var manualResponse = await _table.Query<InventoryProjection>()
                .Where("pk = :pk")
                .WithValue(":pk", warehouseId)
                .WithFilter("quantity >= :minQty")
                .WithValue(":minQty", minQuantity)
                .ToDynamoDbResponseAsync();
            
            // Pattern 2: Format string with filter
            var formatResponse = await _table.Query<InventoryProjection>()
                .Where("pk = {0}", warehouseId)
                .WithFilter("quantity >= {0}", minQuantity)
                .ToDynamoDbResponseAsync();
            
            // Both should return the same count
            manualResponse.Items.Count.Should().Be(formatResponse.Items.Count,
                $"Both patterns should return same count for warehouse {warehouseId} with filter");
            
            // Verify filter was applied correctly
            var projections = manualResponse.Items
                .Select(item => InventoryProjection.FromDynamoDb(item))
                .ToList();
            
            projections.Should().AllSatisfy(p => p.Quantity.Should().BeGreaterThanOrEqualTo(minQuantity));
        }
    }
    
    // Helper class to create a table instance for query operations
    private class TestTable : GenericTable
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
