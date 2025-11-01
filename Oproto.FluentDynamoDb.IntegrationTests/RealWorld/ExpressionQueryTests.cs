using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for expression-based Query operations.
/// These tests verify that LINQ-style expressions work correctly with actual DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ExpressionSupport")]
public class ExpressionQueryTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public ExpressionQueryTests(DynamoDbLocalFixture fixture) : base(fixture)
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
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
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
                CreatedAt = new DateTime(2024, 2, 20, 14, 45, 0, DateTimeKind.Utc),
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
                CreatedAt = new DateTime(2024, 3, 10, 9, 15, 0, DateTimeKind.Utc),
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
                CreatedAt = new DateTime(2024, 4, 5, 16, 20, 0, DateTimeKind.Utc),
                CategoryIds = new HashSet<int> { 1, 2 },
                Tags = new HashSet<string> { "premium", "bestseller" },
                ItemIds = new List<string> { "item-007" },
                Prices = new List<decimal> { 799.99m },
                Metadata = new Dictionary<string, string>
                {
                    ["brand"] = "PhoneCo",
                    ["warranty"] = "1 year"
                }
            },
            new ComplexEntity
            {
                Id = "product-1",
                Type = "accessories",
                Name = "Mouse",
                IsActive = true,
                CreatedAt = new DateTime(2024, 5, 12, 11, 0, 0, DateTimeKind.Utc),
                CategoryIds = new HashSet<int> { 7 },
                Tags = new HashSet<string> { "budget" },
                ItemIds = new List<string> { "item-008" },
                Prices = new List<decimal> { 29.99m }
            }
        };
        
        foreach (var entity in entities)
        {
            var item = ComplexEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    #region Simple Partition Key Queries
    
    [Fact]
    public async Task Query_WithExpressionBasedPartitionKey_ReturnsMatchingItems()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query using expression-based Where()
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(2); // product-1 has 2 items (electronics and accessories)
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.Id.Should().Be("product-1"));
    }
    
    [Fact]
    public async Task Query_WithExpressionBasedPartitionKeyAndSortKey_ReturnsMatchingItem()
    {
        // Arrange
        var productId = "product-1";
        var productType = "electronics";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query using expression with both partition key and sort key
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == productType, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be("product-1");
        entity.Type.Should().Be("electronics");
        entity.Name.Should().Be("Laptop");
    }
    
    #endregion
    
    #region DynamoDB Function Tests
    
    [Fact]
    public async Task Query_WithStartsWithFunction_ReturnsMatchingItems()
    {
        // Arrange
        var productId = "product-1";
        var prefix = "elect";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query using StartsWith function
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type!.StartsWith(prefix), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Type.Should().StartWith(prefix);
    }
    
    [Fact]
    public async Task Query_WithBetweenFunction_ReturnsItemsInRange()
    {
        // Arrange
        var productId = "product-1";
        var lowValue = "a";
        var highValue = "f";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query using Between function
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type!.Between(lowValue, highValue), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(2); // "accessories" and "electronics" are both in range
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Type.Should().NotBeNull();
            e.Type.Should().BeGreaterOrEqualTo(lowValue);
            e.Type.Should().BeLessOrEqualTo(highValue);
        });
    }
    
    [Fact]
    public async Task Query_WithComparisonOperators_ReturnsMatchingItems()
    {
        // Arrange
        var productId = "product-1";
        var compareValue = "d";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query using >= operator
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type! >= compareValue, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1); // Only "electronics" is >= "d"
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Type.Should().Be("electronics");
    }
    
    #endregion
    
    #region Value Capture Tests
    
    [Fact]
    public async Task Query_WithCapturedVariable_UsesVariableValue()
    {
        // Arrange
        var productId = "product-2";
        var productType = "electronics";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with captured variables
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == productType, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.Type.Should().Be(productType);
    }
    
    [Fact]
    public async Task Query_WithClosureCapture_UsesClosureValue()
    {
        // Arrange
        var searchCriteria = new { ProductId = "product-3", ProductType = "furniture" };
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with closure capture
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == searchCriteria.ProductId && x.Type == searchCriteria.ProductType, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(searchCriteria.ProductId);
        entity.Type.Should().Be(searchCriteria.ProductType);
    }
    
    #endregion
    
    #region Complex Expression Tests
    
    [Fact]
    public async Task Query_WithLogicalOperators_CombinesConditionsCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var type1 = "electronics";
        var type2 = "accessories";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with OR operator (note: DynamoDB doesn't support OR in key conditions,
        // so we'll test with multiple queries)
        var response1 = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == type1, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        var response2 = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == type2, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response1.Items.Should().HaveCount(1);
        response2.Items.Should().HaveCount(1);
        
        var entity1 = ComplexEntity.FromDynamoDb<ComplexEntity>(response1.Items[0]);
        entity1.Type.Should().Be(type1);
        
        var entity2 = ComplexEntity.FromDynamoDb<ComplexEntity>(response2.Items[0]);
        entity2.Type.Should().Be(type2);
    }
    
    #endregion
    
    #region Metadata Validation Tests
    
    [Fact]
    public async Task Query_WithoutMetadata_WorksWithoutValidation()
    {
        // Arrange
        var productId = "product-4";
        
        // Act - Query without metadata (validation skipped)
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
    }
    
    #endregion
    
    // Helper class to create a table instance for query operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
