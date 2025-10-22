using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes;

/// <summary>
/// Integration tests for HashSet type serialization and deserialization with DynamoDB.
/// Tests verify that HashSet properties correctly round-trip through DynamoDB Local.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class HashSetIntegrationTests : IntegrationTestBase
{
    public HashSetIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<HashSetTestEntity>();
    }
    
    [Fact]
    public async Task HashSetInt_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "test-hashset-int-1",
            CategoryIds = new HashSet<int> { 1, 2, 3, 5, 8, 13, 21 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().NotBeNull();
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
    }
    
    [Fact]
    public async Task HashSetString_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "test-hashset-string-1",
            Tags = new HashSet<string> { "new", "featured", "sale", "limited-edition", "bestseller" }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Tags.Should().NotBeNull();
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
    }
    
    [Fact]
    public async Task HashSetByteArray_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "test-hashset-binary-1",
            BinaryData = new HashSet<byte[]>
            {
                new byte[] { 0x01, 0x02, 0x03 },
                new byte[] { 0x04, 0x05, 0x06 },
                new byte[] { 0x07, 0x08, 0x09 }
            }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.BinaryData.Should().NotBeNull();
        loaded.BinaryData.Should().HaveCount(entity.BinaryData!.Count);
        
        // Compare byte arrays element by element since HashSet doesn't preserve order
        foreach (var originalBytes in entity.BinaryData)
        {
            loaded.BinaryData.Should().ContainSingle(
                loadedBytes => loadedBytes.SequenceEqual(originalBytes),
                $"binary data {BitConverter.ToString(originalBytes)} should be preserved");
        }
    }
    
    [Fact]
    public async Task HashSet_WithNullValue_LoadsAsNull()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "test-hashset-null-1",
            CategoryIds = null,
            Tags = null,
            BinaryData = null
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeNull();
        loaded.Tags.Should().BeNull();
        loaded.BinaryData.Should().BeNull();
    }
    
    [Fact]
    public async Task HashSet_WithEmptySet_OmitsFromDynamoDBItem()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "test-hashset-empty-1",
            CategoryIds = new HashSet<int>(), // Empty set
            Tags = new HashSet<string>(),     // Empty set
            BinaryData = new HashSet<byte[]>() // Empty set
        };
        
        // Act - Convert to DynamoDB item
        var item = HashSetTestEntity.ToDynamoDb(entity);
        
        // Assert - Empty sets should not be stored in DynamoDB
        // DynamoDB doesn't support empty sets, so they should be omitted
        item.Should().ContainKey("pk", "partition key should always be present");
        item.Should().NotContainKey("category_ids", "empty HashSet<int> should be omitted");
        item.Should().NotContainKey("tags", "empty HashSet<string> should be omitted");
        item.Should().NotContainKey("binary_data", "empty HashSet<byte[]> should be omitted");
    }
}
