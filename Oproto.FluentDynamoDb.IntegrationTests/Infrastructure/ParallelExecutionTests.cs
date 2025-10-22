using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Tests to verify that parallel test execution works correctly with proper isolation.
/// These tests verify that unique table names prevent conflicts between parallel tests.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class ParallelExecutionTests : IntegrationTestBase
{
    private static readonly HashSet<string> _usedTableNames = new();
    private static readonly object _lock = new();
    
    public ParallelExecutionTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<HashSetTestEntity>();
        
        // Verify table name is unique across all parallel test instances
        lock (_lock)
        {
            if (_usedTableNames.Contains(TableName))
            {
                throw new InvalidOperationException(
                    $"Table name collision detected: {TableName}. " +
                    "This indicates parallel execution is not properly isolated.");
            }
            
            _usedTableNames.Add(TableName);
        }
    }
    
    [Fact]
    public async Task ParallelTest1_HasUniqueTableName()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "parallel-test-1",
            CategoryIds = new HashSet<int> { 1, 2, 3 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        Console.WriteLine($"[ParallelTest1] Using table: {TableName}");
    }
    
    [Fact]
    public async Task ParallelTest2_HasUniqueTableName()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "parallel-test-2",
            CategoryIds = new HashSet<int> { 4, 5, 6 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        Console.WriteLine($"[ParallelTest2] Using table: {TableName}");
    }
    
    [Fact]
    public async Task ParallelTest3_HasUniqueTableName()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "parallel-test-3",
            CategoryIds = new HashSet<int> { 7, 8, 9 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        Console.WriteLine($"[ParallelTest3] Using table: {TableName}");
    }
    
    [Fact]
    public async Task ParallelTest4_HasUniqueTableName()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "parallel-test-4",
            Tags = new HashSet<string> { "tag1", "tag2" }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        Console.WriteLine($"[ParallelTest4] Using table: {TableName}");
    }
    
    [Fact]
    public async Task ParallelTest5_HasUniqueTableName()
    {
        // Arrange
        var entity = new HashSetTestEntity
        {
            Id = "parallel-test-5",
            Tags = new HashSet<string> { "tag3", "tag4" }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Tags.Should().BeEquivalentTo(entity.Tags);
        Console.WriteLine($"[ParallelTest5] Using table: {TableName}");
    }
    
    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        
        // Remove from tracking set
        lock (_lock)
        {
            _usedTableNames.Remove(TableName);
        }
    }
}
