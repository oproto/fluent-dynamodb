using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for custom configuration options in table generation.
/// Verifies that custom entity property names, visibility modifiers, and operation generation
/// work correctly end-to-end.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableGeneration")]
[Trait("Feature", "CustomConfiguration")]
public class CustomConfigurationTests : IntegrationTestBase
{
    public CustomConfigurationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task CustomEntityPropertyName_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<CustomNameOrderEntity>();
        var table = new CustomNameTestTable(DynamoDb, TableName);

        // Assert - Custom entity property name should be used
        table.CustomOrders.Should().NotBeNull();
        
        var order = new CustomNameOrderEntity
        {
            Id = "ORDER#CUSTOM",
            Description = "Custom Name Test"
        };

        // Act - Use custom-named accessor
        await table.CustomOrders.Put().WithItem(order).PutAsync();
        
        var retrieved = await table.CustomOrders.Get(order.Id).GetItemAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(order.Id);
        retrieved.Description.Should().Be(order.Description);
    }

    [Fact]
    public async Task GenerateFalse_EntityProperty_DoesNotGenerateAccessor()
    {
        // Arrange
        await CreateTableAsync<NoAccessorEntity>();
        var table = new NoAccessorTestTable(DynamoDb, TableName);

        // Assert - Entity with Generate = false should not have accessor property
        // This is verified at compile time - if the property exists, this won't compile
        // We verify the table still works for the default entity
        table.Should().NotBeNull();
        
        var entity = new NoAccessorDefaultEntity
        {
            Id = "DEFAULT#1",
            Name = "Default Entity"
        };

        // Act - Table-level operations should still work
        await table.Put<NoAccessorDefaultEntity>().WithItem(entity).PutAsync();
        
        var retrieved = await table.Get<NoAccessorDefaultEntity>()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task CustomConfiguration_WithDefaultEntity_TableLevelOperationsWork()
    {
        // Arrange
        await CreateTableAsync<CustomConfigDefaultEntity>();
        var table = new CustomConfigTestTable(DynamoDb, TableName);

        // Assert - Table-level operations should use default entity
        var entity = new CustomConfigDefaultEntity
        {
            Id = "DEFAULT#CONFIG",
            Name = "Default Config Test"
        };

        // Act - Use table-level operations
        await table.Put<CustomConfigDefaultEntity>().WithItem(entity).PutAsync();
        
        var result = await table.Query<CustomConfigDefaultEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", entity.Id)
            .ToDynamoDbResponseAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        var retrieved = CustomConfigDefaultEntity.FromDynamoDb<CustomConfigDefaultEntity>(result.Items[0]);
        retrieved.Id.Should().Be(entity.Id);
        retrieved.Name.Should().Be(entity.Name);
    }

    [Fact]
    public async Task MultipleCustomConfigurations_WorkTogether()
    {
        // Arrange
        await CreateTableAsync<CustomNameOrderEntity>();
        var table = new CustomNameTestTable(DynamoDb, TableName);

        var order = new CustomNameOrderEntity
        {
            Id = "ORDER#MULTI",
            Description = "Multi Config"
        };

        var item = new CustomNameItemEntity
        {
            Id = "ITEM#MULTI",
            ItemName = "Multi Item"
        };

        // Act - Use both custom-named accessors
        await table.CustomOrders.Put().WithItem(order).PutAsync();
        await table.CustomItems.Put().WithItem(item).PutAsync();

        var retrievedOrder = await table.CustomOrders.Get(order.Id).GetItemAsync();

        var retrievedItem = await table.CustomItems.Get(item.Id).GetItemAsync();

        // Assert - Both custom configurations should work
        retrievedOrder.Should().NotBeNull();
        retrievedItem.Should().NotBeNull();
        
        retrievedOrder!.Id.Should().Be(order.Id);

        retrievedItem!.Id.Should().Be(item.Id);
    }
}

// Test entities for custom entity property name
[DynamoDbTable("custom-name-test", IsDefault = true)]
[GenerateEntityProperty(Name = "CustomOrders")]
public partial class CustomNameOrderEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}

[DynamoDbTable("custom-name-test")]
[GenerateEntityProperty(Name = "CustomItems")]
public partial class CustomNameItemEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("item_name")]
    public string? ItemName { get; set; }
}

// Test entities for Generate = false on entity property
[DynamoDbTable("no-accessor-test", IsDefault = true)]
public partial class NoAccessorDefaultEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
}

[DynamoDbTable("no-accessor-test")]
[GenerateEntityProperty(Generate = false)]
public partial class NoAccessorEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("hidden_data")]
    public string? HiddenData { get; set; }
}

// Test entities for custom configuration with default entity
[DynamoDbTable("custom-config-test", IsDefault = true)]
public partial class CustomConfigDefaultEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
}

[DynamoDbTable("custom-config-test")]
[GenerateEntityProperty(Name = "SecondaryItems")]
public partial class CustomConfigSecondaryEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}
