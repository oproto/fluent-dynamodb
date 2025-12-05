using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for entity-specific update builders and express-route methods.
/// Tests verify the new simplified API patterns work correctly with real DynamoDB operations.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "EntitySpecificBuilders")]
public class EntitySpecificBuilderIntegrationTests : IntegrationTestBase
{
    private MultiEntityTestTable _table = null!;
    
    public EntitySpecificBuilderIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        _table = new MultiEntityTestTable(DynamoDb, TableName);
    }
    
    #region 11.1 Entity-Specific Update Builders
    
    [Fact]
    public async Task EntitySpecificBuilder_SimplifiedSet_UpdatesStringProperty()
    {
        // Arrange
        var entity = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#001",
            CustomerName = "John Doe",
            Item = "PENDING"
        };
        
        await _table.Orders.PutAsync(entity);
        
        // Act - Use simplified Set() method
        await _table.Orders.Update("ORDER#001")
            .Set(x => new MultiEntityOrderTestEntityUpdateModel
            {
                Item = "SHIPPED"
            })
            .UpdateAsync();
        
        // Assert
        var loaded = await _table.Orders.GetAsync("ORDER#001");
        loaded.Should().NotBeNull();
        loaded!.Item.Should().Be("SHIPPED");
    }
    
    [Fact]
    public async Task EntitySpecificBuilder_SimplifiedSet_UpdatesNumericProperty()
    {
        // Arrange
        var entity = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#002",
            CustomerName = "Jane Smith",
            TotalAmount = 100.00m
        };
        
        await _table.Orders.PutAsync(entity);
        
        // Act
        await _table.Orders.Update("ORDER#002")
            .Set(x => new MultiEntityOrderTestEntityUpdateModel
            {
                TotalAmount = 250.50m
            })
            .UpdateAsync();
        
        // Assert
        var loaded = await _table.Orders.GetAsync("ORDER#002");
        loaded.Should().NotBeNull();
        loaded!.TotalAmount.Should().Be(250.50m);
    }
    
    #endregion
}
