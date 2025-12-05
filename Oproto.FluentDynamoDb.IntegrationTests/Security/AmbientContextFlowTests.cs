using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.Security;

/// <summary>
/// Integration tests for ambient encryption context flow using AsyncLocal.
/// Validates thread-safety and context isolation between async operations.
/// </summary>
public class AmbientContextFlowTests : IntegrationTestBase
{
    public AmbientContextFlowTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task AmbientContext_FlowsThroughAsyncCalls()
    {
        // Arrange
        var contextId = "tenant-123";

        // Act - Set ambient context
        DynamoDbOperationContext.EncryptionContextId = contextId;

        // Simulate async operations
        await Task.Delay(10);
        var retrievedContext1 = DynamoDbOperationContext.EncryptionContextId;

        await Task.Delay(10);
        var retrievedContext2 = DynamoDbOperationContext.EncryptionContextId;

        // Assert - Context flows through async calls
        retrievedContext1.Should().Be(contextId);
        retrievedContext2.Should().Be(contextId);

        // Cleanup
        DynamoDbOperationContext.EncryptionContextId = null;
    }

    [Fact]
    public async Task AmbientContext_IsolatesBetweenAsyncFlows()
    {
        // Arrange & Act - Start two concurrent async operations with different contexts
        var task1 = Task.Run(async () =>
        {
            DynamoDbOperationContext.EncryptionContextId = "tenant-a";
            await Task.Delay(50);
            return DynamoDbOperationContext.EncryptionContextId;
        });

        var task2 = Task.Run(async () =>
        {
            DynamoDbOperationContext.EncryptionContextId = "tenant-b";
            await Task.Delay(50);
            return DynamoDbOperationContext.EncryptionContextId;
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - Each async flow maintains its own context
        results[0].Should().Be("tenant-a", "first async flow should maintain its context");
        results[1].Should().Be("tenant-b", "second async flow should maintain its context");
    }


    [Fact]
    public async Task AmbientContext_FlowsToNewThreadsByDefault()
    {
        // Arrange
        DynamoDbOperationContext.EncryptionContextId = "main-thread-context";

        // Act - Start a new task on a different thread
        // AsyncLocal flows through Task.Run by default (this is expected .NET behavior)
        var contextInNewThread = await Task.Run(() =>
        {
            // New thread DOES see the main thread's context because AsyncLocal flows through ExecutionContext
            return DynamoDbOperationContext.EncryptionContextId;
        });

        // Assert - AsyncLocal flows to new threads by design
        contextInNewThread.Should().Be("main-thread-context", 
            "AsyncLocal flows through ExecutionContext to new threads by design");
        DynamoDbOperationContext.EncryptionContextId.Should().Be("main-thread-context", 
            "main thread context should be preserved");

        // Cleanup
        DynamoDbOperationContext.EncryptionContextId = null;
    }

    [Fact]
    public async Task AmbientContext_CanBeCleared()
    {
        // Arrange
        DynamoDbOperationContext.EncryptionContextId = "tenant-123";
        DynamoDbOperationContext.EncryptionContextId.Should().Be("tenant-123");

        // Act - Clear context
        DynamoDbOperationContext.EncryptionContextId = null;

        // Assert
        DynamoDbOperationContext.EncryptionContextId.Should().BeNull();

        // Verify it stays null through async operations
        await Task.Delay(10);
        DynamoDbOperationContext.EncryptionContextId.Should().BeNull();
    }

    [Fact]
    public async Task AmbientContext_SupportsNestedAsyncOperations()
    {
        // Arrange
        DynamoDbOperationContext.EncryptionContextId = "outer-context";

        // Act - Nested async operations
        var outerContext = DynamoDbOperationContext.EncryptionContextId;
        
        await Task.Run(async () =>
        {
            // Inner operation sees outer context (AsyncLocal flows through Task.Run)
            var innerContext1 = DynamoDbOperationContext.EncryptionContextId;
            innerContext1.Should().Be("outer-context");

            // Change context in inner operation
            DynamoDbOperationContext.EncryptionContextId = "inner-context";
            await Task.Delay(10);
            
            var innerContext2 = DynamoDbOperationContext.EncryptionContextId;
            innerContext2.Should().Be("inner-context");
        });

        // Assert - Outer context is affected because AsyncLocal flows bidirectionally through Task.Run
        // This is the expected behavior of AsyncLocal in .NET
        DynamoDbOperationContext.EncryptionContextId.Should().Be("inner-context", 
            "AsyncLocal flows bidirectionally through Task.Run, so inner changes affect outer context");

        // Cleanup
        DynamoDbOperationContext.EncryptionContextId = null;
    }

    [Fact]
    public async Task AmbientContext_WorksWithParallelOperations()
    {
        // Arrange & Act - Multiple parallel operations with different contexts
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
        {
            var contextId = $"tenant-{i}";
            DynamoDbOperationContext.EncryptionContextId = contextId;
            
            // Simulate some work
            await Task.Delay(Random.Shared.Next(10, 50));
            
            // Verify context is still correct
            return (Expected: contextId, Actual: DynamoDbOperationContext.EncryptionContextId);
        }));

        var results = await Task.WhenAll(tasks);

        // Assert - All operations maintained their own context
        foreach (var (expected, actual) in results)
        {
            actual.Should().Be(expected, "each parallel operation should maintain its own context");
        }
    }
}
