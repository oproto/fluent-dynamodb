using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Xunit;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for discriminator functionality with real DynamoDB scenarios.
/// Tests attribute-based, sort key pattern, and GSI-specific discriminators.
/// Note: These tests verify discriminator metadata generation and entity serialization.
/// Full projection-based validation will be added when projection spec is complete.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Discriminator")]
public class DiscriminatorIntegrationTests : IntegrationTestBase
{
    public DiscriminatorIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableWithGsiAsync();
    }
    
    private async Task CreateTableWithGsiAsync()
    {
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "gsi1_pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "gsi1_sk", AttributeType = ScalarAttributeType.S }
            },
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "StatusIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "gsi1_pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "gsi1_sk", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(TableName);
    }
    
    #region Task 10.1: Attribute-Based Discriminator Tests
    
    [Fact]
    public async Task AttributeDiscriminator_EntityMetadata_ContainsDiscriminatorInfo()
    {
        // Act - Get entity metadata
        var metadata = UserEntity.GetEntityMetadata();
        
        // Assert - Verify discriminator metadata is present
        metadata.Should().NotBeNull();
        metadata.TableName.Should().Be("test-multi-entity");
        
        // Verify discriminator property exists in metadata
        var discriminatorProp = metadata.Properties
            .FirstOrDefault(p => p.AttributeName == "entity_type");
        
        discriminatorProp.Should().NotBeNull();
    }
    
    [Fact]
    public async Task AttributeDiscriminator_SerializeEntity_IncludesDiscriminatorValue()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "USER#123",
            SortKey = "METADATA",
            EntityType = "USER",
            Name = "John Doe",
            Email = "john@example.com",
            CreatedAt = DateTime.UtcNow
        };
        
        // Act - Serialize to DynamoDB format
        var item = UserEntity.ToDynamoDb(user);
        
        // Assert - Verify discriminator is included
        item.Should().ContainKey("entity_type");
        item["entity_type"].S.Should().Be("USER");
        item.Should().ContainKey("pk");
        item["pk"].S.Should().Be("USER#123");
    }
    
    [Fact]
    public async Task AttributeDiscriminator_RoundTrip_PreservesDiscriminator()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = "USER#456",
            SortKey = "METADATA",
            EntityType = "USER",
            Name = "Jane Smith",
            Email = "jane@example.com"
        };
        
        // Act - Save and load
        var savedUser = await SaveAndLoadAsync(user);
        
        // Assert
        savedUser.EntityType.Should().Be("USER");
        savedUser.Name.Should().Be("Jane Smith");
        savedUser.Email.Should().Be("jane@example.com");
    }
    
    #endregion
    
    #region Task 10.2: Sort Key Pattern Discriminator Tests
    
    [Fact]
    public async Task SortKeyPattern_EntityMetadata_ContainsPatternInfo()
    {
        // Act - Get entity metadata
        var metadata = OrderEntity.GetEntityMetadata();
        
        // Assert - Verify discriminator pattern metadata
        metadata.Should().NotBeNull();
        metadata.TableName.Should().Be("test-multi-entity");
        
        // Verify sort key property exists
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        sortKeyProp.Should().NotBeNull();
        sortKeyProp!.AttributeName.Should().Be("sk");
    }
    
    [Fact]
    public async Task SortKeyPattern_SerializeEntity_MatchesPattern()
    {
        // Arrange
        var order = new OrderEntity
        {
            TenantId = "TENANT#abc",
            OrderKey = "ORDER#001",
            OrderNumber = "ORD-2024-001",
            Total = 99.99m,
            Status = "pending"
        };
        
        // Act - Serialize to DynamoDB format
        var item = OrderEntity.ToDynamoDb(order);
        
        // Assert - Verify sort key matches discriminator pattern
        item.Should().ContainKey("sk");
        item["sk"].S.Should().StartWith("ORDER#");
        item["sk"].S.Should().Be("ORDER#001");
    }
    
    [Fact]
    public async Task SortKeyPattern_RoundTrip_PreservesPattern()
    {
        // Arrange
        var order = new OrderEntity
        {
            TenantId = "TENANT#xyz",
            OrderKey = "ORDER#002",
            OrderNumber = "ORD-2024-002",
            Total = 149.99m,
            Status = "completed"
        };
        
        // Act - Save and load
        var savedOrder = await SaveAndLoadAsync(order);
        
        // Assert
        savedOrder.OrderKey.Should().StartWith("ORDER#");
        savedOrder.OrderNumber.Should().Be("ORD-2024-002");
        savedOrder.Total.Should().Be(149.99m);
    }
    
    [Fact]
    public async Task SortKeyPattern_ContainsPattern_SerializesCorrectly()
    {
        // Arrange
        var product = new ProductEntity
        {
            CategoryId = "CAT#electronics",
            ProductKey = "TENANT#abc#PRODUCT#laptop-001",
            ProductName = "Gaming Laptop",
            Price = 1299.99m,
            InStock = true
        };
        
        // Act - Serialize
        var item = ProductEntity.ToDynamoDb(product);
        
        // Assert - Verify pattern is in sort key
        item.Should().ContainKey("sk");
        item["sk"].S.Should().Contain("#PRODUCT#");
        item["sk"].S.Should().Be("TENANT#abc#PRODUCT#laptop-001");
    }
    
    #endregion
    
    #region Task 10.3: GSI-Specific Discriminator Tests
    
    [Fact]
    public async Task GsiDiscriminator_EntityMetadata_ContainsGsiDiscriminatorInfo()
    {
        // Act - Get entity metadata
        var metadata = InventoryEntity.GetEntityMetadata();
        
        // Assert - Verify GSI discriminator metadata
        metadata.Should().NotBeNull();
        
        // Verify primary discriminator (entity_type)
        var primaryDiscriminator = metadata.Properties
            .FirstOrDefault(p => p.AttributeName == "entity_type");
        primaryDiscriminator.Should().NotBeNull();
        
        // Verify GSI properties exist
        var gsiPkProp = metadata.Properties
            .FirstOrDefault(p => p.AttributeName == "gsi1_pk");
        gsiPkProp.Should().NotBeNull();
        
        var gsiSkProp = metadata.Properties
            .FirstOrDefault(p => p.AttributeName == "gsi1_sk");
        gsiSkProp.Should().NotBeNull();
    }
    
    [Fact]
    public async Task GsiDiscriminator_SerializeEntity_IncludesGsiDiscriminator()
    {
        // Arrange
        var inventory = new InventoryEntity
        {
            WarehouseId = "WH#001",
            ItemKey = "ITEM#laptop-001",
            EntityType = "INVENTORY",
            Status = "IN_STOCK",
            StatusSortKey = "INVENTORY#2024-01-15",
            ItemName = "Laptop",
            Quantity = 50
        };
        
        // Act - Serialize
        var item = InventoryEntity.ToDynamoDb(inventory);
        
        // Assert - Verify both primary and GSI discriminators
        item.Should().ContainKey("entity_type");
        item["entity_type"].S.Should().Be("INVENTORY");
        
        item.Should().ContainKey("gsi1_sk");
        item["gsi1_sk"].S.Should().StartWith("INVENTORY#");
    }
    
    [Fact]
    public async Task GsiDiscriminator_RoundTrip_PreservesGsiPattern()
    {
        // Arrange
        var inventory = new InventoryEntity
        {
            WarehouseId = "WH#002",
            ItemKey = "ITEM#mouse-001",
            EntityType = "INVENTORY",
            Status = "LOW_STOCK",
            StatusSortKey = "INVENTORY#2024-01-16",
            ItemName = "Mouse",
            Quantity = 10
        };
        
        // Act - Save and load
        var savedInventory = await SaveAndLoadAsync(inventory);
        
        // Assert
        savedInventory.EntityType.Should().Be("INVENTORY");
        savedInventory.StatusSortKey.Should().StartWith("INVENTORY#");
        savedInventory.ItemName.Should().Be("Mouse");
        savedInventory.Quantity.Should().Be(10);
    }
    
    #endregion
    
    #region Task 10.4: Multi-Entity Table Query Tests
    
    [Fact]
    public async Task MultiEntityTable_DifferentEntities_SerializeWithCorrectDiscriminators()
    {
        // Arrange - Create multiple entity types
        var user = new UserEntity
        {
            Id = "MULTI#001",
            SortKey = "USER#metadata",
            EntityType = "USER",
            Name = "Alice",
            Email = "alice@example.com"
        };
        
        var order = new OrderEntity
        {
            TenantId = "MULTI#001",
            OrderKey = "ORDER#order-001",
            OrderNumber = "ORD-001",
            Total = 99.99m
        };
        
        var product = new ProductEntity
        {
            CategoryId = "MULTI#001",
            ProductKey = "TENANT#abc#PRODUCT#prod-001",
            ProductName = "Widget",
            Price = 19.99m
        };
        
        // Act - Serialize all entities
        var userItem = UserEntity.ToDynamoDb(user);
        var orderItem = OrderEntity.ToDynamoDb(order);
        var productItem = ProductEntity.ToDynamoDb(product);
        
        // Assert - Each has correct discriminator
        userItem["entity_type"].S.Should().Be("USER");
        orderItem["sk"].S.Should().StartWith("ORDER#");
        productItem["sk"].S.Should().Contain("#PRODUCT#");
    }
    
    [Fact]
    public async Task MultiEntityTable_RoundTrip_AllEntityTypes()
    {
        // Arrange & Act - Save and load different entity types
        var user = new UserEntity
        {
            Id = "RT#001",
            SortKey = "METADATA",
            EntityType = "USER",
            Name = "Bob"
        };
        
        var order = new OrderEntity
        {
            TenantId = "RT#002",
            OrderKey = "ORDER#rt-order",
            OrderNumber = "RT-ORD-001",
            Total = 299.99m
        };
        
        var product = new ProductEntity
        {
            CategoryId = "RT#003",
            ProductKey = "TENANT#xyz#PRODUCT#rt-prod",
            ProductName = "Gadget",
            Price = 49.99m
        };
        
        var savedUser = await SaveAndLoadAsync(user);
        var savedOrder = await SaveAndLoadAsync(order);
        var savedProduct = await SaveAndLoadAsync(product);
        
        // Assert - All discriminators preserved
        savedUser.EntityType.Should().Be("USER");
        savedUser.Name.Should().Be("Bob");
        
        savedOrder.OrderKey.Should().StartWith("ORDER#");
        savedOrder.OrderNumber.Should().Be("RT-ORD-001");
        
        savedProduct.ProductKey.Should().Contain("#PRODUCT#");
        savedProduct.ProductName.Should().Be("Gadget");
    }
    
    [Fact]
    public async Task MultiEntityTable_MixedDiscriminatorStrategies_AllSerializeCorrectly()
    {
        // Arrange - Mix attribute-based, prefix, and contains patterns
        var userWithAttribute = new UserEntity
        {
            Id = "MIX#001",
            SortKey = "METADATA",
            EntityType = "USER",
            Name = "Charlie"
        };
        
        var orderWithPrefix = new OrderEntity
        {
            TenantId = "MIX#002",
            OrderKey = "ORDER#mix-order",
            OrderNumber = "MIX-ORD-001",
            Total = 199.99m
        };
        
        var productWithContains = new ProductEntity
        {
            CategoryId = "MIX#003",
            ProductKey = "TENANT#mix#PRODUCT#item",
            ProductName = "Mixed Item",
            Price = 29.99m
        };
        
        var inventoryWithGsi = new InventoryEntity
        {
            WarehouseId = "MIX#004",
            ItemKey = "ITEM#mix-inv",
            EntityType = "INVENTORY",
            Status = "AVAILABLE",
            StatusSortKey = "INVENTORY#2024-01-20",
            ItemName = "Mixed Inventory",
            Quantity = 100
        };
        
        // Act - Serialize all
        var userItem = UserEntity.ToDynamoDb(userWithAttribute);
        var orderItem = OrderEntity.ToDynamoDb(orderWithPrefix);
        var productItem = ProductEntity.ToDynamoDb(productWithContains);
        var inventoryItem = InventoryEntity.ToDynamoDb(inventoryWithGsi);
        
        // Assert - Each uses correct discriminator strategy
        userItem["entity_type"].S.Should().Be("USER");
        orderItem["sk"].S.Should().StartWith("ORDER#");
        productItem["sk"].S.Should().Contain("#PRODUCT#");
        inventoryItem["entity_type"].S.Should().Be("INVENTORY");
        inventoryItem["gsi1_sk"].S.Should().StartWith("INVENTORY#");
    }
    
    #endregion
}
