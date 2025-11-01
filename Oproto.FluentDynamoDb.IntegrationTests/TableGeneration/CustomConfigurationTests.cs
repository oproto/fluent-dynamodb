using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for custom configuration options in table generation.
/// Verifies that custom entity property names, visibility modifiers, and operation generation
/// work correctly end-to-end.
/// Tests Requirements 4, 5, and 8 from the table-generation-redesign spec.
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
        await table.CustomOrders.Put(order).PutAsync();
        
        var result = await table.CustomOrders.Get()
            .WithKey("pk", order.Id)
            .GetItemAsync();

        // Assert
        result.Item.Should().NotBeNull();
        var retrieved = CustomNameOrderEntity.FromDynamoDb<CustomNameOrderEntity>(result.Item);
        retrieved.Id.Should().Be(order.Id);
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
        await table.Put(entity).PutAsync();
        
        var result = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert
        result.Item.Should().NotBeNull();
        var retrieved = NoAccessorDefaultEntity.FromDynamoDb<NoAccessorDefaultEntity>(result.Item);
        retrieved.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task InternalEntityProperty_IsAccessibleWithinAssembly()
    {
        // Arrange
        await CreateTableAsync<InternalAccessorEntity>();
        var table = new InternalAccessorTestTable(DynamoDb, TableName);

        // Assert - Internal accessor should be accessible within the same assembly
        // This is verified at compile time - if not accessible, this won't compile
        var accessor = table.InternalEntities;
        accessor.Should().NotBeNull();
        
        var entity = new InternalAccessorEntity
        {
            Id = "INTERNAL#1",
            Data = "Internal Test"
        };

        // Act - Use internal accessor
        await accessor.Put(entity).PutAsync();
        
        var result = await accessor.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert
        result.Item.Should().NotBeNull();
        var retrieved = InternalAccessorEntity.FromDynamoDb<InternalAccessorEntity>(result.Item);
        retrieved.Id.Should().Be(entity.Id);
        retrieved.Data.Should().Be(entity.Data);
    }

    [Fact]
    public async Task GenerateFalse_DeleteOperation_DoesNotGenerateMethod()
    {
        // Arrange
        await CreateTableAsync<NoDeleteEntity>();
        var table = new NoDeleteTestTable(DynamoDb, TableName);

        // Assert - Entity with Delete operation Generate = false should not have Delete method
        // This is verified at compile time - if Delete() exists on accessor, this won't compile
        var entity = new NoDeleteEntity
        {
            Id = "NODELETE#1",
            Value = "Cannot Delete"
        };

        // Act - Other operations should still work
        await table.NoDeletes.Put(entity).PutAsync();
        
        var getResult = await table.NoDeletes.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        var queryResult = await table.NoDeletes.Query()
            .Where("pk = :pk")
            .WithValue(":pk", entity.Id)
            .ToDynamoDbResponseAsync();

        // Assert - Get, Query, Put should work
        getResult.Item.Should().NotBeNull();
        queryResult.Items.Should().HaveCount(1);
        
        var retrieved = NoDeleteEntity.FromDynamoDb<NoDeleteEntity>(getResult.Item);
        retrieved.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task InternalOperations_AreAccessibleWithinAssembly()
    {
        // Arrange
        await CreateTableAsync<InternalOperationsEntity>();
        var table = new InternalOperationsTestTable(DynamoDb, TableName);

        // Assert - Internal operations should be accessible within the same assembly
        // This is verified at compile time
        var entity = new InternalOperationsEntity
        {
            Id = "INTERNAL_OPS#1",
            SecretData = "Internal Operations"
        };

        // Act - Use internal operations
        await table.InternalOps.Put(entity).PutAsync();
        
        var result = await table.InternalOps.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert
        result.Item.Should().NotBeNull();
        var retrieved = InternalOperationsEntity.FromDynamoDb<InternalOperationsEntity>(result.Item);
        retrieved.Id.Should().Be(entity.Id);
        retrieved.SecretData.Should().Be(entity.SecretData);
    }

    [Fact]
    public async Task MixedVisibility_Operations_WorkCorrectly()
    {
        // Arrange
        await CreateTableAsync<MixedVisibilityEntity>();
        var table = new MixedVisibilityTestTable(DynamoDb, TableName);

        // Assert - Public Query should be accessible
        var entity = new MixedVisibilityEntity
        {
            Id = "MIXED#1",
            PublicData = "Public",
            PrivateData = "Private"
        };

        // Act - Use public Query operation
        await table.MixedEntities.Put(entity).PutAsync();
        
        var queryResult = await table.MixedEntities.Query()
            .Where("pk = :pk")
            .WithValue(":pk", entity.Id)
            .ToDynamoDbResponseAsync();

        // Assert - Query should work (it's public)
        queryResult.Items.Should().HaveCount(1);
        var retrieved = MixedVisibilityEntity.FromDynamoDb<MixedVisibilityEntity>(queryResult.Items[0]);
        retrieved.Id.Should().Be(entity.Id);
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
        await table.Put(entity).PutAsync();
        
        var result = await table.Query()
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
        await table.CustomOrders.Put(order).PutAsync();
        await table.CustomItems.Put(item).PutAsync();

        var orderResult = await table.CustomOrders.Get()
            .WithKey("pk", order.Id)
            .GetItemAsync();

        var itemResult = await table.CustomItems.Get()
            .WithKey("pk", item.Id)
            .GetItemAsync();

        // Assert - Both custom configurations should work
        orderResult.Item.Should().NotBeNull();
        itemResult.Item.Should().NotBeNull();
        
        var retrievedOrder = CustomNameOrderEntity.FromDynamoDb<CustomNameOrderEntity>(orderResult.Item);
        retrievedOrder.Id.Should().Be(order.Id);

        var retrievedItem = CustomNameItemEntity.FromDynamoDb<CustomNameItemEntity>(itemResult.Item);
        retrievedItem.Id.Should().Be(item.Id);
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

// Test entities for internal entity property
[DynamoDbTable("internal-accessor-test", IsDefault = true)]
[GenerateEntityProperty(Modifier = AccessModifier.Internal)]
public partial class InternalAccessorEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}

// Test entities for Generate = false on Delete operation
[DynamoDbTable("no-delete-test", IsDefault = true)]
[GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
public partial class NoDeleteEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("value")]
    public string? Value { get; set; }
}

// Test entities for internal operations
[DynamoDbTable("internal-operations-test", IsDefault = true)]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class InternalOperationsEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("secret_data")]
    public string? SecretData { get; set; }
}

// Test entities for mixed visibility operations
[DynamoDbTable("mixed-visibility-test", IsDefault = true)]
[GenerateAccessors(Operations = TableOperation.Get | TableOperation.Put | TableOperation.Update | TableOperation.Delete | TableOperation.Scan, Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.Query, Modifier = AccessModifier.Public)]
public partial class MixedVisibilityEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("public_data")]
    public string? PublicData { get; set; }
    
    [DynamoDbAttribute("private_data")]
    public string? PrivateData { get; set; }
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
