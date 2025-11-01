using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for query operations with entities containing advanced types.
/// These tests verify that queries work correctly when filtering on properties
/// that use HashSet, List, and Dictionary types.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class QueryOperationsTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public QueryOperationsTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ComplexEntity>();
        _table = new TestTable(DynamoDb, TableName);
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var entities = new[]
        {
            new ComplexEntity
            {
                Id = "product-1",
                Type = "electronics",
                Name = "Laptop",
                IsActive = true,
                CategoryIds = new HashSet<int> { 1, 2, 3 },
                Tags = new HashSet<string> { "premium", "featured" },
                ItemIds = new List<string> { "item-001", "item-002" },
                Prices = new List<decimal> { 999.99m, 1099.99m },
                Metadata = new Dictionary<string, string>
                {
                    ["brand"] = "TechCorp",
                    ["warranty"] = "2 years"
                }
            },
            new ComplexEntity
            {
                Id = "product-2",
                Type = "electronics",
                Name = "Tablet",
                IsActive = true,
                CategoryIds = new HashSet<int> { 1, 4 },
                Tags = new HashSet<string> { "budget", "popular" },
                ItemIds = new List<string> { "item-003" },
                Prices = new List<decimal> { 299.99m },
                Metadata = new Dictionary<string, string>
                {
                    ["brand"] = "TechCorp",
                    ["warranty"] = "1 year"
                }
            },
            new ComplexEntity
            {
                Id = "product-3",
                Type = "furniture",
                Name = "Desk",
                IsActive = false,
                CategoryIds = new HashSet<int> { 5, 6 },
                Tags = new HashSet<string> { "clearance" },
                ItemIds = new List<string> { "item-004", "item-005", "item-006" },
                Prices = new List<decimal> { 199.99m, 249.99m },
                Metadata = new Dictionary<string, string>
                {
                    ["material"] = "wood",
                    ["color"] = "brown"
                }
            },
            new ComplexEntity
            {
                Id = "product-4",
                Type = "electronics",
                Name = "Phone",
                IsActive = true,
                CategoryIds = new HashSet<int> { 1, 2 },
                Tags = new HashSet<string> { "premium", "bestseller" },
                ItemIds = new List<string> { "item-007" },
                Prices = new List<decimal> { 799.99m },
                Metadata = new Dictionary<string, string>
                {
                    ["brand"] = "PhoneCo",
                    ["warranty"] = "1 year"
                }
            }
        };
        
        foreach (var entity in entities)
        {
            var item = ComplexEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    [Fact]
    public async Task Query_ByPartitionKey_ReturnsAllMatchingItems()
    {
        // Act - Query all electronics products
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "product-1")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be("product-1");
        entity.Name.Should().Be("Laptop");
        entity.CategoryIds.Should().NotBeNull();
        entity.CategoryIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }
    
    [Fact]
    public async Task Query_WithFilterOnBasicProperty_FiltersCorrectly()
    {
        // Act - Query electronics products that are active
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithFilter("#active = :active")
            .WithValue(":pk", "product-2")
            .WithValue(":active", true)
            .WithAttribute("#active", "is_active")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.IsActive.Should().BeTrue();
    }
    
    [Fact]
    public async Task Query_WithSortKeyCondition_ReturnsMatchingItems()
    {
        // Act - Query products with specific type
        var response = await _table.Query()
            .Where("pk = :pk AND sk = :sk")
            .WithValue(":pk", "product-1")
            .WithValue(":sk", "electronics")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Type.Should().Be("electronics");
        entity.Tags.Should().NotBeNull();
        entity.Tags.Should().Contain("premium");
    }
    
    [Fact]
    public async Task Query_WithProjection_ReturnsOnlyRequestedAttributes()
    {
        // Act - Query with projection to get only specific attributes
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "product-1")
            .WithProjection("pk, #name, tags")
            .WithAttribute("#name", "name")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var item = response.Items[0];
        item.Should().ContainKey("pk");
        item.Should().ContainKey("name");
        item.Should().ContainKey("tags");
        
        // Advanced type (tags) should be included in projection
        item["tags"].SS.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task Query_WithLimit_ReturnsLimitedResults()
    {
        // Arrange - Add more items with same partition key
        var additionalEntity = new ComplexEntity
        {
            Id = "product-1",
            Type = "accessories",
            Name = "Mouse",
            CategoryIds = new HashSet<int> { 1 }
        };
        var item = ComplexEntity.ToDynamoDb(additionalEntity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Query with limit
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "product-1")
            .Take(1)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        response.LastEvaluatedKey.Should().NotBeNull();
    }
    
    [Fact]
    public async Task Query_OrderDescending_ReturnsItemsInReverseOrder()
    {
        // Arrange - Add items with different sort keys
        var entities = new[]
        {
            new ComplexEntity { Id = "product-5", Type = "a-first", Name = "First" },
            new ComplexEntity { Id = "product-5", Type = "b-second", Name = "Second" },
            new ComplexEntity { Id = "product-5", Type = "c-third", Name = "Third" }
        };
        
        foreach (var entity in entities)
        {
            var item = ComplexEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Act - Query in descending order
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "product-5")
            .OrderDescending()
            .ToDynamoDbResponseAsync();
        
        // Assert - Items should be in reverse order
        response.Items.Should().HaveCount(3);
        
        var firstEntity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        firstEntity.Type.Should().Be("c-third");
        
        var lastEntity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[2]);
        lastEntity.Type.Should().Be("a-first");
    }
    
    [Fact]
    public async Task Query_WithComplexEntity_PreservesAllAdvancedTypes()
    {
        // Act - Query and verify all advanced types are preserved
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "product-1")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        
        // Verify all advanced types are correctly deserialized
        entity.CategoryIds.Should().NotBeNull();
        entity.CategoryIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        
        entity.Tags.Should().NotBeNull();
        entity.Tags.Should().BeEquivalentTo(new[] { "premium", "featured" });
        
        entity.ItemIds.Should().NotBeNull();
        entity.ItemIds.Should().Equal("item-001", "item-002");
        
        entity.Prices.Should().NotBeNull();
        entity.Prices.Should().Equal(999.99m, 1099.99m);
        
        entity.Metadata.Should().NotBeNull();
        entity.Metadata.Should().ContainKey("brand");
        entity.Metadata!["brand"].Should().Be("TechCorp");
    }
    
    // Helper class to create a table instance for query operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
