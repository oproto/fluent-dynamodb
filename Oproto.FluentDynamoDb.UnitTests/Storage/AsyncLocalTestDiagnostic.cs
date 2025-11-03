using AwesomeAssertions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Diagnostic test to understand AsyncLocal behavior in xUnit
/// </summary>
public class AsyncLocalTestDiagnostic
{
    [Fact]
    public async Task AsyncLocal_SetInAsyncMethod_ShouldBeAccessibleAfterAwait()
    {
        // Arrange
        DynamoDbOperationContext.Clear();

        // Act - Get context returned from the async method
        var context = await SetContextAsync();
        
        // Assert - Check the returned context
        context.Should().NotBeNull("context should be returned from async method");
        context!.OperationType.Should().Be("Test");
    }

    [Fact]
    public async Task AsyncLocal_ReturnedFromAsyncMethod_ShouldWork()
    {
        // Arrange
        DynamoDbOperationContext.Clear();

        // Act - Return context from the async method
        var context = await SetAndReturnContextAsync();
        
        // Assert - Check the returned context
        context.Should().NotBeNull("context should be returned from async method");
        context!.OperationType.Should().Be("Test");
    }

    private async Task<OperationContextData?> SetContextAsync()
    {
        // Simulate what PutAsync does
        await Task.Delay(1); // Simulate async work
        
        DynamoDbOperationContext.Current = new OperationContextData
        {
            OperationType = "Test",
            TableName = "TestTable"
        };
        
        // Return context while still in the same async context
        return DynamoDbOperationContext.Current;
    }

    private async Task<OperationContextData?> SetAndReturnContextAsync()
    {
        // Simulate what PutAsync does
        await Task.Delay(1); // Simulate async work
        
        DynamoDbOperationContext.Current = new OperationContextData
        {
            OperationType = "Test",
            TableName = "TestTable"
        };
        
        // Return the context while still in the async context
        return DynamoDbOperationContext.Current;
    }
}
