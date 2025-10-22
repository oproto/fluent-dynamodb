using System.Diagnostics;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

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
        var uniqueTableName = $"test_perf_{Guid.NewGuid():N}";
        var stopwatch = Stopwatch.StartNew();
        
        // Act - Create a new table
        await CreateTableAsync<ListTestEntity>();
        stopwatch.Stop();
        
        // Assert
        var creationTime = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"[Performance] Table creation time: {creationTime}ms");
        
        creationTime.Should().BeLessThan(2000, 
            "table creation should complete in under 2 seconds");
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
        
        // Assert
        var sequentialTime = sequentialStopwatch.ElapsedMilliseconds;
        var parallelTime = parallelStopwatch.ElapsedMilliseconds;
        var speedup = (double)sequentialTime / parallelTime;
        
        Console.WriteLine($"[Performance] Sequential time: {sequentialTime}ms");
        Console.WriteLine($"[Performance] Parallel time: {parallelTime}ms");
        Console.WriteLine($"[Performance] Speedup: {speedup:F2}x");
        
        // Parallel should be faster (though not necessarily by a fixed factor due to overhead)
        parallelTime.Should().BeLessThanOrEqualTo(sequentialTime, 
            "parallel execution should not be slower than sequential");
    }
}
