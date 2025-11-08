using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for expression-based Scan operations with filters.
/// These tests verify that LINQ-style filter expressions work correctly with Scan operations.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ExpressionSupport")]
public class ExpressionScanTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public ExpressionScanTests(DynamoDbLocalFixture fixture) : base(fixture)
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
                Prices = new List<decimal> { 999.99m, 1099.99m }
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
                Prices = new List<decimal> { 299.99m }
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
                Prices = new List<decimal> { 199.99m, 249.99m }
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
                Prices = new List<decimal> { 799.99m }
            },
            new ComplexEntity
            {
                Id = "product-5",
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
    
    #region Basic Scan with Filter
    
    [Fact]
    public async Task Scan_WithFilterOnBooleanProperty_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with filter on IsActive
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.IsActive == true, metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }
    
    [Fact]
    public async Task Scan_WithFilterOnStringProperty_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with filter on Type
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.Type == "electronics", metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(3); // 3 electronics items
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.Type.Should().Be("electronics"));
    }
    
    #endregion
    
    #region Scan with Various Operators
    
    [Fact]
    public async Task Scan_WithEqualityOperator_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with equality filter
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.Name == "Laptop", metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Name.Should().Be("Laptop");
    }
    
    [Fact]
    public async Task Scan_WithInequalityOperator_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with inequality filter
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.Type != "electronics", metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.Type.Should().NotBe("electronics"));
    }
    
    [Fact]
    public async Task Scan_WithLogicalAndOperator_CombinesConditions()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with AND operator
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(
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
    
    [Fact]
    public async Task Scan_WithLogicalOrOperator_CombinesConditions()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with OR operator
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(
                x => x.Type == "electronics" || x.Type == "furniture", 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Type.Should().Match(t => t == "electronics" || t == "furniture");
        });
    }
    
    [Fact]
    public async Task Scan_WithNegationOperator_InvertsCondition()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with NOT operator
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => !(x.IsActive == false), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
    }
    
    #endregion
    
    #region Scan with DynamoDB Functions
    
    [Fact]
    public async Task Scan_WithStartsWithFunction_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with StartsWith function
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.Name!.StartsWith("L"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Name.Should().StartWith("L");
    }
    
    [Fact]
    public async Task Scan_WithContainsFunction_ReturnsMatchingItems()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with Contains function
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(x => x.Description!.Contains("laptop"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Description.Should().Contain("laptop");
    }
    
    [Fact]
    public async Task Scan_WithBetweenFunction_ReturnsItemsInRange()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        var lowValue = "a";
        var highValue = "f";
        
        // Act - Scan with Between function
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(
                x => x.Type!.Between(lowValue, highValue), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Type.Should().NotBeNull();
            e.Type!.CompareTo(lowValue).Should().BeGreaterThanOrEqualTo(0);
            e.Type!.CompareTo(highValue).Should().BeLessThanOrEqualTo(0);
        });
    }
    
    #endregion
    
    #region Complex Filter Expressions
    
    [Fact]
    public async Task Scan_WithComplexFilterExpression_CombinesMultipleConditions()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with complex filter
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(
                x => x.Type == "electronics" && x.IsActive == true && x.Name!.StartsWith("L"), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Type.Should().Be("electronics");
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().StartWith("L");
    }
    
    [Fact]
    public async Task Scan_WithNestedLogicalOperators_EvaluatesCorrectly()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Scan with nested logical operators
        var response = await _table.Scan<ComplexEntity>()
            .WithFilter<ScanRequestBuilder<ComplexEntity>, ComplexEntity>(
                x => (x.Type == "electronics" || x.Type == "accessories") && x.IsActive == true, 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCountGreaterThan(0);
        
        var entities = response.Items.Select(item => ComplexEntity.FromDynamoDb<ComplexEntity>(item)).ToList();
        entities.Should().AllSatisfy(e => 
        {
            e.Type.Should().Match(t => t == "electronics" || t == "accessories");
            e.IsActive.Should().BeTrue();
        });
    }
    
    #endregion
    
    // Helper class to create a table instance for scan operations
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
