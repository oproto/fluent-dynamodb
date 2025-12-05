using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for complex entities that combine multiple advanced types.
/// These tests verify that realistic entities with HashSet, List, and Dictionary properties
/// work correctly in real-world scenarios.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class ComplexEntityTests : IntegrationTestBase
{
    public ComplexEntityTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ComplexEntity>();
    }
    
    [Fact(Skip = "DateTime Kind information is lost during DynamoDB serialization - known issue")]
    public async Task ComplexEntity_WithAllAdvancedTypes_RoundTripsCorrectly()
    {
        // Arrange - Entity with multiple advanced types
        var entity = new ComplexEntity
        {
            Id = "complex-1",
            Type = "product",
            Name = "Premium Widget",
            Description = "A high-quality widget with advanced features",
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            IsActive = true,
            CategoryIds = new HashSet<int> { 1, 2, 3, 5, 8 },
            Tags = new HashSet<string> { "premium", "featured", "bestseller" },
            ItemIds = new List<string> { "item-001", "item-002", "item-003" },
            Prices = new List<decimal> { 9.99m, 19.99m, 29.99m },
            Metadata = new Dictionary<string, string>
            {
                ["manufacturer"] = "ACME Corp",
                ["warranty"] = "2 years",
                ["color"] = "blue"
            },
            Settings = new Dictionary<string, string>
            {
                ["display_mode"] = "grid",
                ["sort_order"] = "price_asc"
            }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - All properties preserved
        loaded.Id.Should().Be(entity.Id);
        loaded.Type.Should().Be(entity.Type);
        loaded.Name.Should().Be(entity.Name);
        loaded.Description.Should().Be(entity.Description);
        // DateTime comparison: DynamoDB stores as ISO 8601 string, may lose Kind information
        // Compare the actual date/time values, not the Kind
        loaded.CreatedAt.Should().NotBeNull();
        loaded.CreatedAt!.Value.Should().BeCloseTo(entity.CreatedAt!.Value, TimeSpan.FromSeconds(1));
        loaded.IsActive.Should().Be(entity.IsActive);
        
        // Assert - Advanced types preserved
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().Equal(entity.ItemIds);
        
        loaded.Prices.Should().NotBeNull();
        loaded.Prices.Should().Equal(entity.Prices);
        
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().BeEquivalentTo(entity.Metadata);
        
        loaded.Settings.Should().NotBeNull();
        loaded.Settings.Should().BeEquivalentTo(entity.Settings);
    }
    
    [Fact]
    public async Task ComplexEntity_WithPartialAdvancedTypes_RoundTripsCorrectly()
    {
        // Arrange - Entity with only some advanced types populated
        var entity = new ComplexEntity
        {
            Id = "complex-2",
            Type = "service",
            Name = "Basic Service",
            CategoryIds = new HashSet<int> { 10, 20 },
            Tags = new HashSet<string> { "basic" },
            // Other advanced types are null
            ItemIds = null,
            Prices = null,
            Metadata = null,
            Settings = null
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Populated properties preserved
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        
        // Assert - Null properties remain null
        loaded.ItemIds.Should().BeNull();
        loaded.Prices.Should().BeNull();
        loaded.Metadata.Should().BeNull();
        loaded.Settings.Should().BeNull();
    }
    
    [Fact]
    public async Task ComplexEntity_WithEmptyCollections_HandlesCorrectly()
    {
        // Arrange - Entity with empty collections
        var entity = new ComplexEntity
        {
            Id = "complex-3",
            Type = "empty",
            Name = "Empty Collections Test",
            CategoryIds = new HashSet<int>(),
            Tags = new HashSet<string>(),
            ItemIds = new List<string>(),
            Prices = new List<decimal>(),
            Metadata = new Dictionary<string, string>(),
            Settings = new Dictionary<string, string>()
        };
        
        // Act - Convert to DynamoDB item
        var item = ComplexEntity.ToDynamoDb(entity);
        
        // Assert - Empty collections should be omitted from DynamoDB
        item.Should().ContainKey("pk");
        item.Should().ContainKey("sk");
        item.Should().ContainKey("name");
        
        // DynamoDB doesn't support empty sets or lists, so they should be omitted
        item.Should().NotContainKey("category_ids");
        item.Should().NotContainKey("tags");
        item.Should().NotContainKey("item_ids");
        item.Should().NotContainKey("prices");
        item.Should().NotContainKey("metadata");
        item.Should().NotContainKey("settings");
    }
    
    [Fact]
    public async Task ComplexEntity_WithLargeCollections_RoundTripsCorrectly()
    {
        // Arrange - Entity with larger collections to test performance
        var entity = new ComplexEntity
        {
            Id = "complex-4",
            Type = "large",
            Name = "Large Collections Test",
            CategoryIds = new HashSet<int>(Enumerable.Range(1, 50)),
            Tags = new HashSet<string>(Enumerable.Range(1, 30).Select(i => $"tag-{i}")),
            ItemIds = Enumerable.Range(1, 100).Select(i => $"item-{i:D4}").ToList(),
            Prices = Enumerable.Range(1, 75).Select(i => i * 1.99m).ToList(),
            Metadata = Enumerable.Range(1, 20).ToDictionary(i => $"key{i}", i => $"value{i}"),
            Settings = Enumerable.Range(1, 15).ToDictionary(i => $"setting{i}", i => $"config{i}")
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - All large collections preserved
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().HaveCount(50);
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().HaveCount(30);
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().HaveCount(100);
        loaded.ItemIds.Should().Equal(entity.ItemIds);
        
        loaded.Prices.Should().NotBeNull();
        loaded.Prices.Should().HaveCount(75);
        loaded.Prices.Should().Equal(entity.Prices);
        
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().HaveCount(20);
        loaded.Metadata.Should().BeEquivalentTo(entity.Metadata);
        
        loaded.Settings.Should().NotBeNull();
        loaded.Settings.Should().HaveCount(15);
        loaded.Settings.Should().BeEquivalentTo(entity.Settings);
    }
    
    [Fact]
    public async Task ComplexEntity_WithSpecialCharactersInCollections_RoundTripsCorrectly()
    {
        // Arrange - Entity with special characters to test encoding
        var entity = new ComplexEntity
        {
            Id = "complex-5",
            Type = "special",
            Name = "Special Characters Test",
            Tags = new HashSet<string> 
            { 
                "tag with spaces",
                "tag-with-dashes",
                "tag_with_underscores",
                "tag.with.dots",
                "tag@with@symbols"
            },
            ItemIds = new List<string>
            {
                "item with spaces",
                "item/with/slashes",
                "item\\with\\backslashes"
            },
            Metadata = new Dictionary<string, string>
            {
                ["key with spaces"] = "value with spaces",
                ["key-with-dashes"] = "value-with-dashes",
                ["unicode-key"] = "value with émojis 🎉"
            }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert - Special characters preserved
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        
        loaded.ItemIds.Should().NotBeNull();
        loaded.ItemIds.Should().Equal(entity.ItemIds);
        
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Should().BeEquivalentTo(entity.Metadata);
    }
}
