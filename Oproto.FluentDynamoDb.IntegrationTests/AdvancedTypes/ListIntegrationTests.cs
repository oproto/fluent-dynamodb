using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes;

/// <summary>
/// Integration tests for List type serialization and deserialization with DynamoDB.
/// Tests verify that List properties correctly round-trip through DynamoDB Local
/// and that element order is preserved.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class ListIntegrationTests : IntegrationTestBase
{
    public ListIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ListTestEntity>();
    }
    
    [Fact]
    public async Task ListString_RoundTrip_PreservesAllValuesAndOrder()
    {
        // Arrange
        var entity = new ListTestEntity
        {
            Id = "test-list-string-1",
            ItemIds = new List<string> { "item-1", "item-2", "item-3", "item-4", "item-5" }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().Equal(entity.ItemIds, "List order must be preserved");
    }
    
    [Fact]
    public async Task ListInt_RoundTrip_PreservesAllValuesAndOrder()
    {
        // Arrange
        var entity = new ListTestEntity
        {
            Id = "test-list-int-1",
            Quantities = new List<int> { 10, 5, 20, 1, 15, 3 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Quantities.Should().NotBeNull();
        loaded.Quantities.Should().Equal(entity.Quantities, "List order must be preserved");
    }
    
    [Fact]
    public async Task ListDecimal_RoundTrip_PreservesAllValuesAndOrder()
    {
        // Arrange
        var entity = new ListTestEntity
        {
            Id = "test-list-decimal-1",
            Prices = new List<decimal> { 9.99m, 19.99m, 5.50m, 100.00m, 0.99m }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Prices.Should().NotBeNull();
        loaded.Prices.Should().Equal(entity.Prices, "List order must be preserved");
    }
    
    [Fact]
    public async Task List_WithNullValue_LoadsAsNull()
    {
        // Arrange
        var entity = new ListTestEntity
        {
            Id = "test-list-null-1",
            ItemIds = null,
            Quantities = null,
            Prices = null
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.ItemIds.Should().BeNull();
        loaded.Quantities.Should().BeNull();
        loaded.Prices.Should().BeNull();
    }
    
    [Fact]
    public async Task List_WithEmptyList_OmitsFromDynamoDBItem()
    {
        // Arrange
        var entity = new ListTestEntity
        {
            Id = "test-list-empty-1",
            ItemIds = new List<string>(),    // Empty list
            Quantities = new List<int>(),    // Empty list
            Prices = new List<decimal>()     // Empty list
        };
        
        // Act - Convert to DynamoDB item
        var item = ListTestEntity.ToDynamoDb(entity);
        
        // Assert - Empty lists should not be stored in DynamoDB
        // DynamoDB doesn't support empty lists, so they should be omitted
        item.Should().ContainKey("pk", "partition key should always be present");
        item.Should().NotContainKey("item_ids", "empty List<string> should be omitted");
        item.Should().NotContainKey("quantities", "empty List<int> should be omitted");
        item.Should().NotContainKey("prices", "empty List<decimal> should be omitted");
    }
}
