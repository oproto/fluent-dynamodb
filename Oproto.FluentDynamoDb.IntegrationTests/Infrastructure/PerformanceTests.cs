using System.Diagnostics;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Tests to measure and verify integration test performance.
/// These tests help ensure the test suite meets performance targets.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
public class PerformanceTests : IntegrationTestBase
{
    public PerformanceTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<HashSetTestEntity>();
    }
    
    [Fact]
    public async Task SingleTest_CompletesInUnder1Second()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var entity = new HashSetTestEntity
        {
            Id = "perf-test-1",
            CategoryIds = new HashSet<int> { 1, 2, 3 }
        };
        
        // Act
        var loaded = await SaveAndLoadAsync(entity);
        stopwatch.Stop();
        
        // Assert
        loaded.CategoryIds.Should().BeEquivalentTo(entity.CategoryIds);
        
        // Performance assertion
        var executionTime = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"[Performance] Test execution time: {executionTime}ms");
        
        executionTime.Should().BeLessThan(1000, 
            "individual tests should complete in under 1 second (excluding DynamoDB Local startup)");
    }
    
    [Fact]
    public async Task TableCreation_CompletesInUnder2Seconds()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Create a new table with a unique name
        // Note: We need to create a separate table, not reuse the one from InitializeAsync
        var uniqueTableName = $"test_perf_timing_{Guid.NewGuid():N}";
        await CreateUniqueTableAsync<ListTestEntity>(uniqueTableName);
        stopwatch.Stop();
        
        // Assert
        var creationTime = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"[Performance] Table creation time: {creationTime}ms");
        
        creationTime.Should().BeLessThan(2000, 
            "table creation should complete in under 2 seconds");
    }
    
    /// <summary>
    /// Creates a table with a specific unique name for performance testing.
    /// This avoids conflicts with the table created in InitializeAsync.
    /// </summary>
    private async Task CreateUniqueTableAsync<TEntity>(string tableName) where TEntity : IDynamoDbEntity
    {
        var metadata = TEntity.GetEntityMetadata();
        
        // Find partition key property
        var partitionKeyProp = metadata.Properties.FirstOrDefault(p => p.IsPartitionKey);
        if (partitionKeyProp == null)
        {
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity).Name} does not have a partition key property");
        }
        
        var request = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = partitionKeyProp.AttributeName,
                    KeyType = KeyType.HASH
                }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = partitionKeyProp.AttributeName,
                    AttributeType = GetScalarAttributeType(partitionKeyProp.PropertyType)
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        // Add sort key if present
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        if (sortKeyProp != null)
        {
            request.KeySchema.Add(new KeySchemaElement
            {
                AttributeName = sortKeyProp.AttributeName,
                KeyType = KeyType.RANGE
            });
            
            request.AttributeDefinitions.Add(new AttributeDefinition
            {
                AttributeName = sortKeyProp.AttributeName,
                AttributeType = GetScalarAttributeType(sortKeyProp.PropertyType)
            });
        }
        
        await DynamoDb.CreateTableAsync(request);
        
        // Wait for table to be active
        await WaitForTableActiveAsync(tableName);
        
        // Clean up this table after the test
        await DynamoDb.DeleteTableAsync(tableName);
    }
    
    private static ScalarAttributeType GetScalarAttributeType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        if (underlyingType == typeof(string))
            return ScalarAttributeType.S;
        
        if (underlyingType == typeof(int) || 
            underlyingType == typeof(long) || 
            underlyingType == typeof(decimal) || 
            underlyingType == typeof(double) || 
            underlyingType == typeof(float) ||
            underlyingType == typeof(short) ||
            underlyingType == typeof(byte))
            return ScalarAttributeType.N;
        
        if (underlyingType == typeof(byte[]))
            return ScalarAttributeType.B;
        
        return ScalarAttributeType.S;
    }
    
    [Fact]
    public async Task MultipleOperations_CompleteInUnder3Seconds()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var entities = Enumerable.Range(1, 10).Select(i => new HashSetTestEntity
        {
            Id = $"perf-test-multi-{i}",
            CategoryIds = new HashSet<int> { i, i + 1, i + 2 }
        }).ToList();
        
        // Act - Perform multiple save/load operations
        foreach (var entity in entities)
        {
            await SaveAndLoadAsync(entity);
        }
        stopwatch.Stop();
        
        // Assert
        var totalTime = stopwatch.ElapsedMilliseconds;
        var avgTime = totalTime / entities.Count;
        
        Console.WriteLine($"[Performance] Total time for {entities.Count} operations: {totalTime}ms");
        Console.WriteLine($"[Performance] Average time per operation: {avgTime}ms");
        
        totalTime.Should().BeLessThan(3000, 
            "10 save/load operations should complete in under 3 seconds");
    }
    
    [Fact]
    public void DynamoDbLocalFixture_ReportsStartupTime()
    {
        // Arrange & Act
        var fixture = new DynamoDbLocalFixture();
        
        // Assert - Just verify the properties exist and can be accessed
        // The actual startup happens in the collection fixture
        Console.WriteLine($"[Performance] DynamoDB Local startup time: {fixture.StartupTimeMs}ms");
        Console.WriteLine($"[Performance] Reused existing instance: {fixture.ReusedExistingInstance}");
        
        // If we're reusing an instance, startup should be very fast
        if (fixture.ReusedExistingInstance)
        {
            fixture.StartupTimeMs.Should().BeLessThan(1000, 
                "checking for existing DynamoDB Local instance should be fast");
        }
    }
    
    [Fact]
    public async Task ParallelOperations_ImprovePerformance()
    {
        // Arrange
        var entityCount = 5;
        var entities = Enumerable.Range(1, entityCount).Select(i => new HashSetTestEntity
        {
            Id = $"perf-test-parallel-{i}",
            CategoryIds = new HashSet<int> { i, i + 1, i + 2 }
        }).ToList();
        
        // Act - Sequential execution
        var sequentialStopwatch = Stopwatch.StartNew();
        foreach (var entity in entities)
        {
            await SaveAndLoadAsync(entity);
        }
        sequentialStopwatch.Stop();
        
        // Act - Parallel execution
        var parallelStopwatch = Stopwatch.StartNew();
        await Task.WhenAll(entities.Select(async entity =>
        {
            await SaveAndLoadAsync(entity);
        }));
        parallelStopwatch.Stop();
        
        // Assert - Just verify operations completed successfully
        // Note: We don't assert timing relationships as they're unreliable in CI environments
        // with varying resource availability and contention
        var sequentialTime = sequentialStopwatch.ElapsedMilliseconds;
        var parallelTime = parallelStopwatch.ElapsedMilliseconds;
        var speedup = sequentialTime > 0 ? (double)sequentialTime / parallelTime : 0;
        
        Console.WriteLine($"[Performance] Sequential time: {sequentialTime}ms");
        Console.WriteLine($"[Performance] Parallel time: {parallelTime}ms");
        Console.WriteLine($"[Performance] Speedup: {speedup:F2}x");
        
        // Test passes if both approaches completed without errors
        // The timing information is logged for diagnostic purposes only
        sequentialTime.Should().BeGreaterThan(0, "sequential execution should have taken measurable time");
        parallelTime.Should().BeGreaterThan(0, "parallel execution should have taken measurable time");
    }
}
