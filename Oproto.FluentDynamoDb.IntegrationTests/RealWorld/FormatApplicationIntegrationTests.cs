using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for format string application in LINQ expressions.
/// These tests verify that format specifications from DynamoDbAttribute are correctly
/// applied when translating LINQ expressions to DynamoDB queries.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "FormatApplication")]
public class FormatApplicationIntegrationTests : IntegrationTestBase
{
    private TestTable _table = null!;
    
    public FormatApplicationIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<FormattedEntity>();
        _table = new TestTable(DynamoDb, TableName);
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var entities = new[]
        {
            new FormattedEntity
            {
                Id = "item-1",
                Type = "product",
                Name = "Laptop",
                CreatedDate = new DateTime(2024, 10, 24),
                UpdatedAt = new DateTime(2024, 10, 24, 15, 30, 45),
                Amount = 1234.56m,
                Price = 999.9999m,
                Rating = 4.75,
                OrderNumber = 123,
                Quantity = 10
            },
            new FormattedEntity
            {
                Id = "item-2",
                Type = "product",
                Name = "Tablet",
                CreatedDate = new DateTime(2024, 10, 25),
                UpdatedAt = new DateTime(2024, 10, 25, 10, 15, 30),
                Amount = 567.89m,
                Price = 499.5000m,
                Rating = 4.25,
                OrderNumber = 456,
                Quantity = 5
            },
            new FormattedEntity
            {
                Id = "item-3",
                Type = "product",
                Name = "Phone",
                CreatedDate = new DateTime(2024, 10, 26),
                UpdatedAt = new DateTime(2024, 10, 26, 8, 0, 0),
                Amount = 789.12m,
                Price = 699.9900m,
                Rating = 4.50,
                OrderNumber = 789,
                Quantity = 15
            }
        };
        
        foreach (var entity in entities)
        {
            var item = FormattedEntity.ToDynamoDb(entity);
            await DynamoDb.PutItemAsync(TableName, item);
        }
    }
    
    [Fact]
    public async Task Query_WithFormattedDateTimeProperty_AppliesDateOnlyFormat()
    {
        // Arrange - CreatedDate has Format = "yyyy-MM-dd"
        var date = new DateTime(2024, 10, 24, 15, 30, 45); // Time component should be ignored
        
        // Act - Query using LINQ expression with formatted DateTime
        var response = await _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.CreatedDate == date)
            .ToDynamoDbResponseAsync();
        
        // Assert - Should find the item because format strips time
        response.Items.Should().HaveCount(1);
        
        var entity = FormattedEntity.FromDynamoDb<FormattedEntity>(response.Items[0]);
        entity.Id.Should().Be("item-1");
        entity.CreatedDate.Should().Be(new DateTime(2024, 10, 24));
    }
    
    [Fact]
    public async Task Query_WithFormattedDateTimeProperty_VerifiesFormattedValueSentToDynamoDB()
    {
        // Arrange - CreatedDate has Format = "yyyy-MM-dd"
        var date = new DateTime(2024, 10, 24, 23, 59, 59); // Different time, same date
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.CreatedDate == date)
            .ToRequest();
        
        // Assert - Verify the formatted value is "2024-10-24" (date only, no time)
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-10-24");
    }
    
    [Fact]
    public async Task Query_WithFormattedDateTimeISO8601_AppliesFullFormat()
    {
        // Arrange - UpdatedAt has Format = "yyyy-MM-ddTHH:mm:ss"
        var dateTime = new DateTime(2024, 10, 24, 15, 30, 45);
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.UpdatedAt == dateTime)
            .ToRequest();
        
        // Assert - Verify the formatted value includes date and time
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-10-24T15:30:45");
    }
    
    [Fact]
    public async Task Query_WithFormattedDecimalProperty_AppliesTwoDecimalPlaces()
    {
        // Arrange - Amount has Format = "F2"
        var amount = 1234.5678m; // More precision than format
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.Amount == amount)
            .ToRequest();
        
        // Assert - Verify the formatted value is rounded to 2 decimal places
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("1234.57");
    }
    
    [Fact]
    public async Task Query_WithFormattedDecimalProperty_AppliesFourDecimalPlaces()
    {
        // Arrange - Price has Format = "F4"
        var price = 999.999999m; // More precision than format
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.Price == price)
            .ToRequest();
        
        // Assert - Verify the formatted value is rounded to 4 decimal places
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("1000.0000");
    }
    
    [Fact]
    public async Task Query_WithFormattedDoubleProperty_AppliesFormat()
    {
        // Arrange - Rating has Format = "F2"
        var rating = 4.7567;
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.Rating == rating)
            .ToRequest();
        
        // Assert - Verify the formatted value is rounded to 2 decimal places
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("4.76");
    }
    
    [Fact]
    public async Task Query_WithFormattedIntegerProperty_AppliesZeroPadding()
    {
        // Arrange - OrderNumber has Format = "D8"
        var orderNumber = 123;
        
        // Act - Build request to inspect the formatted value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.OrderNumber == orderNumber)
            .ToRequest();
        
        // Assert - Verify the formatted value is zero-padded to 8 digits
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("00000123");
    }
    
    [Fact]
    public async Task Query_WithUnformattedProperty_UsesDefaultSerialization()
    {
        // Arrange - Quantity has no format specified
        var quantity = 10;
        
        // Act - Build request to inspect the value
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.Quantity == quantity)
            .ToRequest();
        
        // Assert - Verify the value uses default number serialization
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].N.Should().Be("10");
    }
    
    [Fact]
    public async Task Query_WithFormattedProperty_EndToEnd_ReturnsCorrectResults()
    {
        // Arrange - Query with formatted date
        var date = new DateTime(2024, 10, 25);
        
        // Act - Execute query end-to-end
        var response = await _table.Query<FormattedEntity>(x => x.Id == "item-2" && x.CreatedDate == date)
            .ToDynamoDbResponseAsync();
        
        // Assert - Should find the matching item
        response.Items.Should().HaveCount(1);
        
        var entity = FormattedEntity.FromDynamoDb<FormattedEntity>(response.Items[0]);
        entity.Id.Should().Be("item-2");
        entity.Name.Should().Be("Tablet");
        entity.CreatedDate.Should().Be(new DateTime(2024, 10, 25));
    }
    
    [Fact]
    public async Task Scan_WithFormattedDateTimeProperty_AppliesFormat()
    {
        // Arrange - CreatedDate has Format = "yyyy-MM-dd"
        var date = new DateTime(2024, 10, 26);
        
        // Act - Build scan request with formatted filter
        var request = _table.Scan<FormattedEntity>(x => x.CreatedDate == date)
            .ToRequest();
        
        // Assert - Verify the formatted value
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("2024-10-26");
    }
    
    [Fact]
    public async Task Scan_WithFormattedDecimalProperty_AppliesFormat()
    {
        // Arrange - Amount has Format = "F2"
        var amount = 789.123m;
        
        // Act - Build scan request with formatted filter
        var request = _table.Scan<FormattedEntity>(x => x.Amount == amount)
            .ToRequest();
        
        // Assert - Verify the formatted value is rounded to 2 decimal places
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("789.12");
    }
    
    [Fact]
    public async Task Scan_WithFormattedProperty_EndToEnd_ReturnsCorrectResults()
    {
        // Arrange - Scan with formatted decimal
        var amount = 567.89m;
        
        // Act - Execute scan end-to-end
        var response = await _table.Scan<FormattedEntity>(x => x.Amount == amount)
            .ToDynamoDbResponseAsync();
        
        // Assert - Should find the matching item
        response.Items.Should().HaveCount(1);
        
        var entity = FormattedEntity.FromDynamoDb<FormattedEntity>(response.Items[0]);
        entity.Id.Should().Be("item-2");
        entity.Amount.Should().Be(567.89m);
    }
    
    [Fact]
    public async Task Query_WithMultipleFormattedProperties_AppliesAllFormats()
    {
        // Arrange - Multiple formatted properties
        var date = new DateTime(2024, 10, 24);
        var amount = 1234.56m;
        
        // Act - Build request with multiple formatted properties
        var request = _table.Query<FormattedEntity>(x => 
            x.Id == "item-1" && x.CreatedDate == date && x.Amount == amount)
            .ToRequest();
        
        // Assert - Verify both formatted values
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-10-24");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p2");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("1234.56");
    }
    
    [Fact]
    public async Task Query_WithFormattedPropertyInComparison_AppliesFormat()
    {
        // Arrange - Amount has Format = "F2", testing with greater than
        var amount = 600.00m;
        
        // Act - Build request with comparison operator
        var request = _table.Query<FormattedEntity>(x => x.Id == "item-2" && x.Amount > amount)
            .ToRequest();
        
        // Assert - Verify the formatted value in comparison
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("600.00");
    }
    
    [Fact]
    public async Task Scan_WithFormattedPropertyInBetween_AppliesFormatToBothBounds()
    {
        // Arrange - Amount has Format = "F2"
        var lowAmount = 500.00m;
        var highAmount = 800.00m;
        
        // Act - Build scan request with BETWEEN
        var request = _table.Scan<FormattedEntity>(x => x.Amount.Between(lowAmount, highAmount))
            .ToRequest();
        
        // Assert - Verify both bounds are formatted
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("500.00");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("800.00");
    }
    
    [Fact]
    public async Task Query_WithFormattedProperty_ResultsDeserializeCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 10, 24);
        
        // Act - Execute query and deserialize results
        var response = await _table.Query<FormattedEntity>(x => x.Id == "item-1" && x.CreatedDate == date)
            .ToDynamoDbResponseAsync();
        
        // Assert - Verify deserialization preserves original values
        response.Items.Should().HaveCount(1);
        
        var entity = FormattedEntity.FromDynamoDb<FormattedEntity>(response.Items[0]);
        entity.CreatedDate.Should().Be(new DateTime(2024, 10, 24));
        entity.Amount.Should().Be(1234.56m);
        entity.Price.Should().Be(999.9999m);
        entity.Rating.Should().Be(4.75);
        entity.OrderNumber.Should().Be(123);
    }
    
    // Helper class to create a table instance for query and scan operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
