using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for update operations with entities containing advanced collection types.
/// These tests verify that HashSet, List, and Dictionary properties can be updated correctly
/// using DynamoDB update expressions.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class UpdateOperationsTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public UpdateOperationsTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ComplexEntity>();
        _table = new TestTable(DynamoDb, TableName);
    }
    
    [Fact]
    public async Task Update_HashSetProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-1",
            Type = "product",
            Name = "Test Product",
            CategoryIds = new HashSet<int> { 1, 2, 3 }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update the HashSet property
        var newCategoryIds = new HashSet<int> { 4, 5, 6, 7 };
        var categoryIdsAttributeValue = new AttributeValue
        {
            NS = newCategoryIds.Select(x => x.ToString()).ToList()
        };
        
        await _table.Update
            .WithKey("pk", "update-test-1")
            .WithKey("sk", "product")
            .Set("SET category_ids = :categoryIds")
            .WithValue(":categoryIds", categoryIdsAttributeValue)
            .ExecuteAsync();
        
        // Assert - Verify the update
        var loaded = await SaveAndLoadAsync(entity);
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(newCategoryIds);
    }
    
    [Fact]
    public async Task Update_HashSetStringProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-2",
            Type = "product",
            Name = "Test Product",
            Tags = new HashSet<string> { "old-tag1", "old-tag2" }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update the HashSet<string> property
        var newTags = new HashSet<string> { "new-tag1", "new-tag2", "new-tag3" };
        var tagsAttributeValue = new AttributeValue
        {
            SS = newTags.ToList()
        };
        
        await _table.Update
            .WithKey("pk", "update-test-2")
            .WithKey("sk", "product")
            .Set("SET tags = :tags")
            .WithValue(":tags", tagsAttributeValue)
            .ExecuteAsync();
        
        // Assert - Verify the update
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-2" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(newTags);
    }
    
    [Fact]
    public async Task Update_ListProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-3",
            Type = "product",
            Name = "Test Product",
            ItemIds = new List<string> { "item-001", "item-002" }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update the List property
        var newItemIds = new List<string> { "item-003", "item-004", "item-005" };
        var itemIdsAttributeValue = new AttributeValue
        {
            L = newItemIds.Select(id => new AttributeValue { S = id }).ToList()
        };
        
        await _table.Update
            .WithKey("pk", "update-test-3")
            .WithKey("sk", "product")
            .Set("SET item_ids = :itemIds")
            .WithValue(":itemIds", itemIdsAttributeValue)
            .ExecuteAsync();
        
        // Assert - Verify the update
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-3" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().Equal(newItemIds);
    }
    
    [Fact]
    public async Task Update_ListDecimalProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-4",
            Type = "product",
            Name = "Test Product",
            Prices = new List<decimal> { 10.99m, 20.99m }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update the List<decimal> property
        var newPrices = new List<decimal> { 15.99m, 25.99m, 35.99m };
        var pricesAttributeValue = new AttributeValue
        {
            L = newPrices.Select(price => new AttributeValue { N = price.ToString() }).ToList()
        };
        
        await _table.Update
            .WithKey("pk", "update-test-4")
            .WithKey("sk", "product")
            .Set("SET prices = :prices")
            .WithValue(":prices", pricesAttributeValue)
            .ExecuteAsync();
        
        // Assert - Verify the update
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-4" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.Prices.Should().NotBeNull();
        loaded.Prices.Should().Equal(newPrices);
    }
    
    [Fact]
    public async Task Update_DictionaryProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-5",
            Type = "product",
            Name = "Test Product",
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update the Dictionary property
        var newMetadata = new Dictionary<string, string>
        {
            ["key3"] = "value3",
            ["key4"] = "value4",
            ["key5"] = "value5"
        };
        
        var metadataAttributeValue = new AttributeValue
        {
            M = newMetadata.ToDictionary(
                kvp => kvp.Key,
                kvp => new AttributeValue { S = kvp.Value })
        };
        
        await _table.Update
            .WithKey("pk", "update-test-5")
            .WithKey("sk", "product")
            .Set("SET metadata = :metadata")
            .WithValue(":metadata", metadataAttributeValue)
            .ExecuteAsync();
        
        // Assert - Verify the update
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-5" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().BeEquivalentTo(newMetadata);
    }
    
    [Fact]
    public async Task Update_MultipleCollectionProperties_UpdatesAllSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-6",
            Type = "product",
            Name = "Test Product",
            CategoryIds = new HashSet<int> { 1, 2 },
            Tags = new HashSet<string> { "old" },
            ItemIds = new List<string> { "item-001" },
            Metadata = new Dictionary<string, string> { ["old"] = "value" }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update multiple collection properties at once
        var newCategoryIds = new HashSet<int> { 10, 20, 30 };
        var newTags = new HashSet<string> { "new", "updated" };
        var newItemIds = new List<string> { "item-100", "item-200" };
        var newMetadata = new Dictionary<string, string> { ["new"] = "updated-value" };
        
        await _table.Update
            .WithKey("pk", "update-test-6")
            .WithKey("sk", "product")
            .Set("SET category_ids = :categoryIds, tags = :tags, item_ids = :itemIds, metadata = :metadata")
            .WithValue(":categoryIds", new AttributeValue { NS = newCategoryIds.Select(x => x.ToString()).ToList() })
            .WithValue(":tags", new AttributeValue { SS = newTags.ToList() })
            .WithValue(":itemIds", new AttributeValue { L = newItemIds.Select(id => new AttributeValue { S = id }).ToList() })
            .WithValue(":metadata", new AttributeValue 
            { 
                M = newMetadata.ToDictionary(kvp => kvp.Key, kvp => new AttributeValue { S = kvp.Value }) 
            })
            .ExecuteAsync();
        
        // Assert - Verify all updates
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-6" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(newCategoryIds);
        
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(newTags);
        
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().Equal(newItemIds);
        
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().BeEquivalentTo(newMetadata);
    }
    
    [Fact]
    public async Task Update_RemoveCollectionProperty_RemovesSuccessfully()
    {
        // Arrange - Create initial entity with collections
        var entity = new ComplexEntity
        {
            Id = "update-test-7",
            Type = "product",
            Name = "Test Product",
            CategoryIds = new HashSet<int> { 1, 2, 3 },
            Tags = new HashSet<string> { "tag1", "tag2" }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Remove collection properties
        await _table.Update
            .WithKey("pk", "update-test-7")
            .WithKey("sk", "product")
            .Remove("REMOVE category_ids, tags")
            .ExecuteAsync();
        
        // Assert - Verify properties were removed
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-7" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.CategoryIds.Should().BeNull();
        loaded.Tags.Should().BeNull();
    }
    
    [Fact]
    public async Task Update_ConditionalUpdateOnCollection_SucceedsWhenConditionMet()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "update-test-8",
            Type = "product",
            Name = "Test Product",
            CategoryIds = new HashSet<int> { 1, 2, 3 }
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Conditional update (only if category_ids exists)
        var newCategoryIds = new HashSet<int> { 10, 20 };
        
        await _table.Update
            .WithKey("pk", "update-test-8")
            .WithKey("sk", "product")
            .Set("SET category_ids = :categoryIds")
            .Where("attribute_exists(category_ids)")
            .WithValue(":categoryIds", new AttributeValue { NS = newCategoryIds.Select(x => x.ToString()).ToList() })
            .ExecuteAsync();
        
        // Assert - Verify the update succeeded
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "update-test-8" },
                ["sk"] = new AttributeValue { S = "product" }
            }
        });
        
        var loaded = ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(newCategoryIds);
    }
    
    [Fact]
    public async Task Update_ConditionalUpdateOnCollection_FailsWhenConditionNotMet()
    {
        // Arrange - Create initial entity without category_ids
        var entity = new ComplexEntity
        {
            Id = "update-test-9",
            Type = "product",
            Name = "Test Product"
            // No CategoryIds set
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act & Assert - Conditional update should fail
        var newCategoryIds = new HashSet<int> { 10, 20 };
        
        var act = async () => await _table.Update
            .WithKey("pk", "update-test-9")
            .WithKey("sk", "product")
            .Set("SET category_ids = :categoryIds")
            .Where("attribute_exists(category_ids)")
            .WithValue(":categoryIds", new AttributeValue { NS = newCategoryIds.Select(x => x.ToString()).ToList() })
            .ExecuteAsync();
        
        await act.Should().ThrowAsync<ConditionalCheckFailedException>();
    }
    
    // Helper class to create a table instance for update operations
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
}
