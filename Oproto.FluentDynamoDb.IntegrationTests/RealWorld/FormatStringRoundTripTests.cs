using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for format string round-trip operations.
/// These tests verify that format strings are correctly applied during serialization (ToDynamoDb)
/// and parsing (FromDynamoDb), and that values maintain consistency across PutItem and UpdateItem operations.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "FormatStrings")]
public class FormatStringRoundTripTests : IntegrationTestBase
{
    private TestTable _table = null!;
    
    public FormatStringRoundTripTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<FormattedEntity>();
        _table = new TestTable(DynamoDb, TableName);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithDateOnlyFormat_PreservesDateOnly()
    {
        // Arrange - DateTime with date-only format
        var entity = new FormattedEntity
        {
            Id = "format-test-1",
            Type = "test",
            CreatedDate = new DateTime(2024, 11, 9, 15, 30, 45) // Time component should be stripped
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify date is preserved, time is stripped
        loaded.CreatedDate.Should().NotBeNull();
        loaded.CreatedDate!.Value.Date.Should().Be(new DateTime(2024, 11, 9));
        loaded.CreatedDate.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithISO8601Format_PreservesDateAndTime()
    {
        // Arrange - DateTime with ISO 8601 format (no milliseconds)
        var entity = new FormattedEntity
        {
            Id = "format-test-2",
            Type = "test",
            UpdatedAt = new DateTime(2024, 11, 9, 15, 30, 45, 123) // Milliseconds should be stripped
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify date and time preserved, milliseconds stripped
        loaded.UpdatedAt.Should().NotBeNull();
        loaded.UpdatedAt!.Value.Should().Be(new DateTime(2024, 11, 9, 15, 30, 45));
    }
    
    [Fact]
    public async Task SaveAndLoad_WithDecimalF2Format_RoundsToTwoDecimalPlaces()
    {
        // Arrange - Decimal with F2 format
        var entity = new FormattedEntity
        {
            Id = "format-test-3",
            Type = "test",
            Amount = 1234.5678m // Should be rounded to 1234.57
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify rounding to 2 decimal places
        loaded.Amount.Should().NotBeNull();
        loaded.Amount!.Value.Should().Be(1234.57m);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithDecimalF4Format_RoundsToFourDecimalPlaces()
    {
        // Arrange - Decimal with F4 format
        var entity = new FormattedEntity
        {
            Id = "format-test-4",
            Type = "test",
            Price = 999.999999m // Should be rounded to 1000.0000
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify rounding to 4 decimal places
        loaded.Price.Should().NotBeNull();
        loaded.Price!.Value.Should().Be(1000.0000m);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithDoubleF2Format_RoundsToTwoDecimalPlaces()
    {
        // Arrange - Double with F2 format
        var entity = new FormattedEntity
        {
            Id = "format-test-5",
            Type = "test",
            Rating = 4.7567 // Should be rounded to 4.76
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify rounding to 2 decimal places
        loaded.Rating.Should().NotBeNull();
        loaded.Rating!.Value.Should().BeApproximately(4.76, 0.01);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithIntegerD8Format_PreservesValueWithZeroPadding()
    {
        // Arrange - Integer with D8 format (zero-padding)
        var entity = new FormattedEntity
        {
            Id = "format-test-6",
            Type = "test",
            OrderNumber = 123 // Should be stored as "00000123"
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify value is preserved (padding is for storage only)
        loaded.OrderNumber.Should().NotBeNull();
        loaded.OrderNumber!.Value.Should().Be(123);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithMultipleFormattedProperties_PreservesAllFormats()
    {
        // Arrange - Entity with multiple formatted properties
        var entity = new FormattedEntity
        {
            Id = "format-test-7",
            Type = "test",
            CreatedDate = new DateTime(2024, 11, 9, 10, 20, 30),
            UpdatedAt = new DateTime(2024, 11, 9, 15, 45, 50, 789),
            Amount = 5678.9123m,
            Price = 1234.567890m,
            Rating = 3.456789,
            OrderNumber = 42
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify all formats are applied correctly
        loaded.CreatedDate!.Value.Should().Be(new DateTime(2024, 11, 9));
        loaded.UpdatedAt!.Value.Should().Be(new DateTime(2024, 11, 9, 15, 45, 50));
        loaded.Amount!.Value.Should().Be(5678.91m);
        loaded.Price!.Value.Should().Be(1234.5679m);
        loaded.Rating!.Value.Should().BeApproximately(3.46, 0.01);
        loaded.OrderNumber!.Value.Should().Be(42);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithUnformattedProperties_UsesDefaultSerialization()
    {
        // Arrange - Entity with unformatted properties
        var entity = new FormattedEntity
        {
            Id = "format-test-8",
            Type = "test",
            Name = "Test Product",
            Quantity = 100
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify unformatted properties use default serialization
        loaded.Name.Should().Be("Test Product");
        loaded.Quantity.Should().Be(100);
    }
    
    [Fact]
    public async Task UpdateItem_WithFormattedProperty_AppliesFormatConsistently()
    {
        // Arrange - Create initial entity
        var entity = new FormattedEntity
        {
            Id = "format-test-9",
            Type = "test",
            Amount = 100.00m
        };
        await SaveAndLoadAsync(entity);
        
        // Act - Update using UpdateItem with formatted property
        var updateRequest = new UpdateItemRequestBuilder<FormattedEntity>(DynamoDb)
            .ForTable(TableName)
            .SetKey(key =>
            {
                key["pk"] = new AttributeValue { S = "format-test-9" };
                key["sk"] = new AttributeValue { S = "test" };
            })
            .Set<FormattedEntity, FormattedEntityUpdateExpressions, FormattedEntityUpdateModel>(x => new FormattedEntityUpdateModel
            {
                Amount = 250.5678m // Should be formatted to 250.57
            })
            .ToUpdateItemRequest();
        
        await DynamoDb.UpdateItemAsync(updateRequest);
        
        // Load and verify
        var loaded = await LoadEntityAsync("format-test-9", "test");
        
        // Assert - Verify format is applied in UpdateItem
        loaded.Amount.Should().NotBeNull();
        loaded.Amount!.Value.Should().Be(250.57m);
    }
    
    [Fact]
    public async Task UpdateItem_WithMultipleFormattedProperties_AppliesAllFormats()
    {
        // Arrange - Create initial entity
        var entity = new FormattedEntity
        {
            Id = "format-test-10",
            Type = "test",
            Amount = 100.00m,
            Rating = 3.00
        };
        await SaveAndLoadAsync(entity);
        
        // Act - Update multiple formatted properties
        var updateRequest = new UpdateItemRequestBuilder<FormattedEntity>(DynamoDb)
            .ForTable(TableName)
            .SetKey(key =>
            {
                key["pk"] = new AttributeValue { S = "format-test-10" };
                key["sk"] = new AttributeValue { S = "test" };
            })
            .Set<FormattedEntity, FormattedEntityUpdateExpressions, FormattedEntityUpdateModel>(x => new FormattedEntityUpdateModel
            {
                Amount = 999.9999m, // Should be formatted to 1000.00
                Rating = 4.567 // Should be formatted to 4.57
            })
            .ToUpdateItemRequest();
        
        await DynamoDb.UpdateItemAsync(updateRequest);
        
        // Load and verify
        var loaded = await LoadEntityAsync("format-test-10", "test");
        
        // Assert - Verify all formats are applied
        loaded.Amount!.Value.Should().Be(1000.00m);
        loaded.Rating!.Value.Should().BeApproximately(4.57, 0.01);
    }
    
    [Fact]
    public async Task PutItemAndUpdateItem_ProduceSameFormattedValues()
    {
        // Arrange - Create two entities with same values
        var putEntity = new FormattedEntity
        {
            Id = "format-test-11a",
            Type = "test",
            Amount = 1234.5678m,
            CreatedDate = new DateTime(2024, 11, 9, 10, 20, 30)
        };
        
        var updateEntity = new FormattedEntity
        {
            Id = "format-test-11b",
            Type = "test",
            Amount = 0m,
            CreatedDate = DateTime.MinValue
        };
        
        // Act - Save first entity with PutItem
        await SaveAndLoadAsync(putEntity);
        
        // Save second entity and update with same values
        await SaveAndLoadAsync(updateEntity);
        var updateRequest = new UpdateItemRequestBuilder<FormattedEntity>(DynamoDb)
            .ForTable(TableName)
            .SetKey(key =>
            {
                key["pk"] = new AttributeValue { S = "format-test-11b" };
                key["sk"] = new AttributeValue { S = "test" };
            })
            .Set<FormattedEntity, FormattedEntityUpdateExpressions, FormattedEntityUpdateModel>(x => new FormattedEntityUpdateModel
            {
                Amount = 1234.5678m,
                CreatedDate = new DateTime(2024, 11, 9, 10, 20, 30)
            })
            .ToUpdateItemRequest();
        
        await DynamoDb.UpdateItemAsync(updateRequest);
        
        // Load both entities
        var putLoaded = await LoadEntityAsync("format-test-11a", "test");
        var updateLoaded = await LoadEntityAsync("format-test-11b", "test");
        
        // Assert - Verify both operations produce same formatted values
        putLoaded.Amount.Should().Be(updateLoaded.Amount);
        putLoaded.CreatedDate.Should().Be(updateLoaded.CreatedDate);
    }
    
    [Fact]
    public async Task SaveAndLoad_WithNullFormattedProperties_HandlesNullCorrectly()
    {
        // Arrange - Entity with null formatted properties
        var entity = new FormattedEntity
        {
            Id = "format-test-12",
            Type = "test",
            CreatedDate = null,
            Amount = null,
            Rating = null,
            OrderNumber = null
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify null values are preserved
        loaded.CreatedDate.Should().BeNull();
        loaded.Amount.Should().BeNull();
        loaded.Rating.Should().BeNull();
        loaded.OrderNumber.Should().BeNull();
    }
    
    [Fact]
    public async Task SaveAndLoad_WithEdgeCaseValues_HandlesCorrectly()
    {
        // Arrange - Entity with edge case values
        var entity = new FormattedEntity
        {
            Id = "format-test-13",
            Type = "test",
            Amount = 0.01m, // Very small value
            Price = 9999999.9999m, // Large value
            Rating = 0.0, // Zero
            OrderNumber = 99999999 // Max for D8 format
        };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Verify edge cases are handled correctly
        loaded.Amount!.Value.Should().Be(0.01m);
        loaded.Price!.Value.Should().Be(9999999.9999m);
        loaded.Rating!.Value.Should().Be(0.0);
        loaded.OrderNumber!.Value.Should().Be(99999999);
    }
    
    // Helper method to load an entity by key
    private async Task<FormattedEntity> LoadEntityAsync(string id, string type)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = id },
            ["sk"] = new AttributeValue { S = type }
        };
        
        var response = await DynamoDb.GetItemAsync(TableName, key);
        return FormattedEntity.FromDynamoDb<FormattedEntity>(response.Item);
    }
    
    // Helper class to create a table instance for operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
