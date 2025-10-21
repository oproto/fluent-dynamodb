using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes;

/// <summary>
/// Integration tests for Dictionary type serialization and deserialization with DynamoDB.
/// Tests verify that Dictionary properties correctly round-trip through DynamoDB Local.
/// </summary>
[Collection("DynamoDB Local")]
public class DictionaryIntegrationTests : IntegrationTestBase
{
    public DictionaryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<DictionaryTestEntity>();
    }
    
    [Fact]
    public async Task DictionaryStringString_RoundTrip_PreservesAllKeyValuePairs()
    {
        // Arrange
        var entity = new DictionaryTestEntity
        {
            Id = "test-dictionary-1",
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
                ["key3"] = "value3",
                ["description"] = "A test entity with metadata",
                ["category"] = "testing"
            }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().BeEquivalentTo(entity.Metadata);
    }
    
    [Fact]
    public async Task Dictionary_WithNullValue_LoadsAsNull()
    {
        // Arrange
        var entity = new DictionaryTestEntity
        {
            Id = "test-dictionary-null-1",
            Metadata = null,
            Settings = null
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Metadata.Should().BeNull();
        loaded.Settings.Should().BeNull();
    }
    
    [Fact]
    public async Task Dictionary_WithEmptyDictionary_OmitsFromDynamoDBItem()
    {
        // Arrange
        var entity = new DictionaryTestEntity
        {
            Id = "test-dictionary-empty-1",
            Metadata = new Dictionary<string, string>(),  // Empty dictionary
            Settings = new Dictionary<string, string>()   // Empty dictionary
        };
        
        // Act - Convert to DynamoDB item
        var item = DictionaryTestEntity.ToDynamoDb(entity);
        
        // Assert - Empty dictionaries should not be stored in DynamoDB
        // DynamoDB doesn't support empty maps, so they should be omitted
        item.Should().ContainKey("pk", "partition key should always be present");
        item.Should().NotContainKey("metadata", "empty Dictionary should be omitted");
        item.Should().NotContainKey("settings", "empty Dictionary should be omitted");
    }
}
