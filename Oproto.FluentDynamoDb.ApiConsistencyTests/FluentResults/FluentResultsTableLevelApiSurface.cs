using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults table-level convenience methods.
/// These tests validate that table-level Result-returning methods are generated correctly
/// when [UseFluentResults] is applied to an entity.
/// Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2
/// </summary>
public class FluentResultsTableLevelApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task GetAsyncResult_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Table-level GetAsyncResult with composite key ===
        var result = await table.GetAsyncResult("pk1", "sk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entity = result.Value;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task GetAsyncResult_EntityAccessor_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Entity accessor GetAsyncResult with composite key ===
        var result = await table.FluentResultsEntitys.GetAsyncResult("pk1", "sk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entity = result.Value;
        }
    }

    
    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Table-level DeleteAsyncResult with composite key ===
        var result = await table.DeleteAsyncResult("pk1", "sk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Delete succeeded
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_EntityAccessor_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Entity accessor DeleteAsyncResult with composite key ===
        var result = await table.FluentResultsEntitys.DeleteAsyncResult("pk1", "sk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Delete succeeded
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncResult_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);
        var entity = new FluentResultsEntity { PartitionKey = "pk1", SortKey = "sk1", Name = "Test", Count = 5 };

        // === Table-level PutAsyncResult ===
        var result = await table.PutAsyncResult(entity);
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Put succeeded
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncResult_EntityAccessor_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);
        var entity = new FluentResultsEntity { PartitionKey = "pk1", SortKey = "sk1", Name = "Test", Count = 5 };

        // === Entity accessor PutAsyncResult ===
        var result = await table.FluentResultsEntitys.PutAsyncResult(entity);
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Put succeeded
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task QueryAsyncResult_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Table-level QueryAsyncResult with lambda expression ===
        var result = await table.QueryAsyncResult(x => x.PartitionKey == "pk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entities = result.Value;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task QueryAsyncResult_EntityAccessor_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // === Entity accessor QueryAsyncResult with lambda expression ===
        var result = await table.FluentResultsEntitys.QueryAsyncResult(x => x.PartitionKey == "pk1");
        
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entities = result.Value;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task TraditionalAsyncMethods_ShouldNotExist_WhenHideGeneratedAsyncMethodsIsTrue()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsTableTable table = new FluentResultsTableTable(client, "fluentResultsTable", options: null);

        // When [UseFluentResults] is applied with HideGeneratedAsyncMethods = true (default),
        // traditional async methods (GetAsync, DeleteAsync) should NOT be generated at table level.
        // This test validates that only Result-returning methods are available.
        
        // The following lines would cause compilation errors if uncommented,
        // proving that traditional async methods are not generated:
        // await table.GetAsync("pk1", "sk1");  // Should NOT compile
        // await table.DeleteAsync("pk1", "sk1");  // Should NOT compile
        
        // Only Result-returning methods should be available:
        var getResult = await table.GetAsyncResult("pk1", "sk1");
        var deleteResult = await table.DeleteAsyncResult("pk1", "sk1");
        
        // Verify the results are the correct types
        global::FluentResults.Result<FluentResultsEntity?> typedGetResult = getResult;
        global::FluentResults.Result typedDeleteResult = deleteResult;
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task BothMethodTypes_ShouldExist_WhenHideGeneratedAsyncMethodsIsFalse()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        FluentResultsBothTableTable table = new FluentResultsBothTableTable(client, "fluentResultsBothTable", options: null);

        // When [UseFluentResults(HideGeneratedAsyncMethods = false)] is applied,
        // both traditional async methods AND Result-returning methods should be generated.
        
        // Traditional async methods should exist:
        var entity = await table.GetAsync("pk1", "sk1");
        await table.DeleteAsync("pk1", "sk1");
        
        // Result-returning methods should also exist:
        var getResult = await table.GetAsyncResult("pk1", "sk1");
        var deleteResult = await table.DeleteAsyncResult("pk1", "sk1");
        
        // Verify the results are the correct types
        global::FluentResults.Result<FluentResultsBothEntity?> typedGetResult = getResult;
        global::FluentResults.Result typedDeleteResult = deleteResult;
    }
}
