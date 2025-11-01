using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for single-entity table generation.
/// Verifies that tables with a single entity generate correctly and operations work end-to-end.
/// Tests Requirements 1 and 2 from the table-generation-redesign spec.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableGeneration")]
public class SingleEntityTableTests : IntegrationTestBase
{
    public SingleEntityTableTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SingleEntity_GeneratesTableClass_WithCorrectName()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);

        // Assert - Table class should be generated and instantiable
        table.Should().NotBeNull();
        table.TableName.Should().Be(TableName);
        table.Client.Should().Be(DynamoDb);
    }

    [Fact]
    public async Task SingleEntity_GetOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "Test Entity",
            Value = 42
        };

        // Act - Put item
        await table.Put(entity)
            .PutAsync();

        // Act - Get item
        var result = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert
        result.Should().NotBeNull();
        result.Item.Should().NotBeNull();
        
        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(result.Item);
        retrieved.Id.Should().Be(entity.Id);
        retrieved.Name.Should().Be(entity.Name);
        retrieved.Value.Should().Be(entity.Value);
    }

    [Fact]
    public async Task SingleEntity_QueryOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entities = new[]
        {
            new SingleEntityTestEntity { Id = "test-id-1", Name = "Entity 1", Value = 10 },
            new SingleEntityTestEntity { Id = "test-id-2", Name = "Entity 2", Value = 20 },
            new SingleEntityTestEntity { Id = "test-id-3", Name = "Entity 3", Value = 30 }
        };

        foreach (var entity in entities)
        {
            await table.Put(entity).PutAsync();
        }

        // Act - Query for specific item
        var result = await table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "test-id-1")
            .ToDynamoDbResponseAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        
        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(result.Items[0]);
        retrieved.Id.Should().Be("test-id-1");
        retrieved.Name.Should().Be("Entity 1");
    }

    [Fact]
    public async Task SingleEntity_PutOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "New Entity",
            Value = 100
        };

        // Act
        await table.Put(entity).PutAsync();

        // Assert - Verify item was saved
        var getResult = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        getResult.Item.Should().NotBeNull();
        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(getResult.Item);
        retrieved.Should().BeEquivalentTo(entity);
    }

    [Fact]
    public async Task SingleEntity_DeleteOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "To Delete",
            Value = 50
        };

        await table.Put(entity).PutAsync();

        // Act - Delete the item
        await table.Delete()
            .WithKey("pk", entity.Id)
            .DeleteAsync();

        // Assert - Verify item was deleted
        var getResult = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        getResult.Item.Should().BeNull();
    }

    [Fact]
    public async Task SingleEntity_UpdateOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "Original Name",
            Value = 10
        };

        await table.Put(entity).PutAsync();

        // Act - Update the item
        await table.Update()
            .WithKey("pk", entity.Id)
            .Set("#name = :name")
            .WithValue(":name", "Updated Name")
            .WithAttribute("#name", "name")
            .UpdateAsync();

        // Assert - Verify item was updated
        var getResult = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(getResult.Item);
        retrieved.Name.Should().Be("Updated Name");
        retrieved.Value.Should().Be(10); // Unchanged
    }

    [Fact]
    public async Task SingleEntity_ScanOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entities = new[]
        {
            new SingleEntityTestEntity { Id = "test-id-1", Name = "Entity 1", Value = 10 },
            new SingleEntityTestEntity { Id = "test-id-2", Name = "Entity 2", Value = 20 },
            new SingleEntityTestEntity { Id = "test-id-3", Name = "Entity 3", Value = 30 }
        };

        foreach (var entity in entities)
        {
            await table.Put(entity).PutAsync();
        }

        // Act - Scan all items
        var result = await table.Scan()
            .ToDynamoDbResponseAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        
        var retrievedEntities = result.Items
            .Select(item => SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(item))
            .ToList();
        
        retrievedEntities.Should().HaveCount(3);
        retrievedEntities.Should().Contain(e => e.Id == "test-id-1");
        retrievedEntities.Should().Contain(e => e.Id == "test-id-2");
        retrievedEntities.Should().Contain(e => e.Id == "test-id-3");
    }

    [Fact]
    public async Task SingleEntity_DefaultEntityBehavior_DoesNotRequireIsDefault()
    {
        // Arrange - Single entity should work without IsDefault = true
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "Default Test",
            Value = 99
        };

        // Act - All operations should work without explicit IsDefault
        await table.Put(entity).PutAsync();
        
        var getResult = await table.Get()
            .WithKey("pk", entity.Id)
            .GetItemAsync();

        // Assert - Table-level operations should use the single entity as default
        getResult.Item.Should().NotBeNull();
        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(getResult.Item);
        retrieved.Should().BeEquivalentTo(entity);
    }

    [Fact]
    public async Task SingleEntity_TableLevelOperations_UseEntityType()
    {
        // Arrange
        await CreateTableAsync<SingleEntityTestEntity>();
        var table = new SingleEntityTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityTestEntity
        {
            Id = "test-id-1",
            Name = "Type Test",
            Value = 123
        };

        // Act - Use table-level operations (not entity accessor)
        await table.Put(entity).PutAsync();
        
        var queryResult = await table.Query()
            .Where("pk = :pk")
            .WithValue(":pk", entity.Id)
            .ToDynamoDbResponseAsync();

        // Assert - Operations should return correct entity type
        queryResult.Items.Should().HaveCount(1);
        var retrieved = SingleEntityTestEntity.FromDynamoDb<SingleEntityTestEntity>(queryResult.Items[0]);
        retrieved.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task SingleEntity_WithSortKey_WorksCorrectly()
    {
        // Arrange
        await CreateTableAsync<SingleEntityWithSortKeyTestEntity>();
        var table = new SingleEntityWithSortKeyTestEntityTable(DynamoDb, TableName);
        
        var entity = new SingleEntityWithSortKeyTestEntity
        {
            PartitionKey = "pk-1",
            SortKey = "sk-1",
            Data = "Test Data"
        };

        // Act
        await table.Put(entity).PutAsync();
        
        var result = await table.Get()
            .WithKey("pk", entity.PartitionKey)
            .WithKey("sk", entity.SortKey)
            .GetItemAsync();

        // Assert
        result.Item.Should().NotBeNull();
        var retrieved = SingleEntityWithSortKeyTestEntity.FromDynamoDb<SingleEntityWithSortKeyTestEntity>(result.Item);
        retrieved.PartitionKey.Should().Be(entity.PartitionKey);
        retrieved.SortKey.Should().Be(entity.SortKey);
        retrieved.Data.Should().Be(entity.Data);
    }
}

/// <summary>
/// Test entity for single-entity table tests.
/// </summary>
[DynamoDbTable("single-entity-test")]
public partial class SingleEntityTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("value")]
    public int? Value { get; set; }
}

/// <summary>
/// Test entity with sort key for single-entity table tests.
/// </summary>
[DynamoDbTable("single-entity-sortkey-test")]
public partial class SingleEntityWithSortKeyTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}
