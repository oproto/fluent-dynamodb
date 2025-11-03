using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for expression-based filter operations on Query and Scan.
/// These tests verify that LINQ-style filter expressions work correctly with actual DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ExpressionSupport")]
public class ExpressionFilterTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public ExpressionFilterTests(DynamoDbLocalFixture fixture) : base(fixture)
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
                Description = "High-performance laptop",
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
                Description = "Portable tablet device",
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
                Description = "Wooden office desk",
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
                Description = "Smartphone with advanced features",
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
                Description = "Wireless mouse",
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
    
    #region Query with Filter on Non-Key Attributes
    
    [Fact]
    public async Task Query_WithFilterOnBooleanProperty_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with filter on IsActive (non-key attribute)
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(2); // Both items with product-1 are active
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }
    
    [Fact]
    public async Task Query_WithFilterOnStringProperty_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with filter on Name
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Name == "Laptop", metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Name.Should().Be("Laptop");
    }
    
    [Fact]
    public async Task Query_WithFilterUsingStartsWith_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with filter using StartsWith
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Name!.StartsWith("Lap"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Name.Should().StartWith("Lap");
    }
    
    [Fact]
    public async Task Query_WithFilterUsingContains_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with filter using Contains
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Description!.Contains("laptop"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Description.Should().Contain("laptop");
    }
    
    #endregion
    
    #region Complex Filter Expressions
    
    [Fact]
    public async Task Query_WithComplexFilterExpression_CombinesConditionsCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with complex filter (multiple conditions with AND)
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(
                x => x.IsActive == true && x.Name!.StartsWith("L"), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().StartWith("L");
    }
    
    [Fact]
    public async Task Query_WithFilterOnMultipleProperties_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with filter on multiple properties
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(
                x => x.IsActive == true && x.Name == "Laptop" && x.Description!.Contains("performance"), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().Be("Laptop");
        entity.Description.Should().Contain("performance");
    }
    
    [Fact]
    public async Task Query_WithFilterUsingNegation_FiltersCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Query with NOT operator
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => !(x.Name == "Mouse"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Name.Should().NotBe("Mouse");
    }
    
    #endregion
    
    #region Mixing Where and WithFilter
    
    [Fact]
    public async Task Query_MixingExpressionWhereAndFilter_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Mix expression-based Where() and WithFilter()
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Id.Should().Be(productId);
            e.IsActive.Should().BeTrue();
        });
    }
    
    [Fact]
    public async Task Query_MixingExpressionWhereAndSortKey_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var productType = "electronics";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Expression Where with both keys, then filter
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == productType, 
                metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.Type.Should().Be(productType);
        entity.IsActive.Should().BeTrue();
    }
    
    #endregion
    
    #region Filter with Various Operators
    
    [Fact]
    public async Task Query_WithFilterUsingComparisonOperators_FiltersCorrectly()
    {
        // Arrange - Query all electronics products
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Use scan to get all items, then filter
        var response = await _table.Scan()
            .WithFilter<ScanRequestBuilder, ComplexEntity>(
                x => x.Type == "electronics" && x.IsActive == true, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Type.Should().Be("electronics");
            e.IsActive.Should().BeTrue();
        });
    }
    
    #endregion
    
    // Helper class to create a table instance for query operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        public ScanRequestBuilder<ComplexEntity> Scan() => 
            new ScanRequestBuilder<ComplexEntity>(DynamoDbClient).ForTable(Name);
    }
}
