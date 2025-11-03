using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for mixing expression-based and string-based query methods.
/// These tests verify that expression-based and string-based methods can be used together.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ExpressionSupport")]
public class ExpressionMixedTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public ExpressionMixedTests(DynamoDbLocalFixture fixture) : base(fixture)
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
                Tags = new HashSet<string> { "premium", "featured" }
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
                Tags = new HashSet<string> { "budget", "popular" }
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
                Tags = new HashSet<string> { "clearance" }
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
                Tags = new HashSet<string> { "premium", "bestseller" }
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
                Tags = new HashSet<string> { "budget" }
            }
        };
        
        foreach (var entity in entities)
        {
            var item = ComplexEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    #region Expression Where + String WithFilter
    
    [Fact]
    public async Task Query_ExpressionWhereWithStringFilter_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Expression-based Where() + string-based WithFilter()
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter("#active = :active")
            .WithAttribute("#active", "is_active")
            .WithValue(":active", true)
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
    public async Task Query_ExpressionWhereWithFormatStringFilter_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Expression-based Where() + format string WithFilter()
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter("#active = {0}", true)
            .WithAttribute("#active", "is_active")
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
    
    #endregion
    
    #region String Where + Expression WithFilter
    
    [Fact]
    public async Task Query_StringWhereWithExpressionFilter_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - String-based Where() + expression-based WithFilter()
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", productId)
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
    public async Task Query_FormatStringWhereWithExpressionFilter_WorksCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Format string Where() + expression-based WithFilter()
        var response = await _table.Query()
            .Where("pk = {0}", productId)
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
    
    #endregion
    
    #region Multiple Calls of Each Type
    
    [Fact]
    public async Task Query_MultipleExpressionFilters_CombinesWithAnd()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Multiple expression-based WithFilter() calls
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Name!.StartsWith("L"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().StartWith("L");
    }
    
    [Fact]
    public async Task Query_MixingMultipleExpressionAndStringFilters_CombinesCorrectly()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Mix expression and string filters
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .WithFilter("#name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "Laptop")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().Be("Laptop");
    }
    
    [Fact]
    public async Task Scan_MixingExpressionAndStringFilters_WorksCorrectly()
    {
        // Arrange
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Mix expression and string filters on Scan
        var response = await _table.Scan()
            .WithFilter<ScanRequestBuilder, ComplexEntity>(x => x.Type == "electronics", metadata)
            .WithFilter("#active = :active")
            .WithAttribute("#active", "is_active")
            .WithValue(":active", true)
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
    
    #region Parameter Name Uniqueness
    
    [Fact]
    public async Task Query_MixedCallsWithManyParameters_MaintainsUniqueParameterNames()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Multiple calls with different parameter types
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .WithFilter("#name = :customName")
            .WithAttribute("#name", "name")
            .WithValue(":customName", "Laptop")
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Type == "electronics", metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().Be("Laptop");
        entity.Type.Should().Be("electronics");
    }
    
    [Fact]
    public async Task Query_ComplexMixedScenario_WorksEndToEnd()
    {
        // Arrange
        var productId = "product-1";
        var productType = "electronics";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Complex scenario mixing all styles
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(
                x => x.Id == productId && x.Type == productType, 
                metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.IsActive == true, metadata)
            .WithFilter("#desc = {0}", "High-performance laptop")
            .WithAttribute("#desc", "description")
            .WithFilter<QueryRequestBuilder, ComplexEntity>(x => x.Name!.StartsWith("L"), metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.Type.Should().Be(productType);
        entity.IsActive.Should().BeTrue();
        entity.Description.Should().Be("High-performance laptop");
        entity.Name.Should().StartWith("L");
    }
    
    #endregion
    
    #region Backward Compatibility
    
    [Fact]
    public async Task Query_PureStringBasedApproach_StillWorksAsExpected()
    {
        // Arrange
        var productId = "product-1";
        
        // Act - Pure string-based approach (existing functionality)
        var response = await _table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", productId)
            .WithFilter("#active = :active AND begins_with(#name, :prefix)")
            .WithAttribute("#active", "is_active")
            .WithAttribute("#name", "name")
            .WithValue(":active", true)
            .WithValue(":prefix", "L")
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().StartWith("L");
    }
    
    [Fact]
    public async Task Query_PureFormatStringApproach_WorksAsExpected()
    {
        // Arrange
        var productId = "product-1";
        
        // Act - Pure format string approach
        var response = await _table.Query()
            .Where("pk = {0}", productId)
            .WithFilter("#active = {0}", true)
            .WithAttribute("#active", "is_active")
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
    public async Task Query_PureExpressionBasedApproach_WorksAsExpected()
    {
        // Arrange
        var productId = "product-1";
        var metadata = ComplexEntity.GetEntityMetadata();
        
        // Act - Pure expression-based approach
        var response = await _table.Query()
            .Where<QueryRequestBuilder, ComplexEntity>(x => x.Id == productId, metadata)
            .WithFilter<QueryRequestBuilder, ComplexEntity>(
                x => x.IsActive == true && x.Name!.StartsWith("L"), 
                metadata)
            .ToDynamoDbResponseAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        
        var entity = ComplexEntity.FromDynamoDb<ComplexEntity>(response.Items[0]);
        entity.Id.Should().Be(productId);
        entity.IsActive.Should().BeTrue();
        entity.Name.Should().StartWith("L");
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
