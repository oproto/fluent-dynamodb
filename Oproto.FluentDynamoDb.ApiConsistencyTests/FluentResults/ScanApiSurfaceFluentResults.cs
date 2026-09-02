using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Scan operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.6, 3.3, 4.4
/// </summary>
public class ScanApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_ScannableTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);

        // === Basic Scan (no filter) ===
        var result = await table.Scan().ToListAsyncResult();
        
        // Scan on Entity accessor
        result = await table.ScannableEntitys.Scan().ToListAsyncResult();
        
        // === Lambda Expression Style (Preferred) ===
        result = await table.Scan(x => x.Age >= 21).ToListAsyncResult();
        
        // Lambda on Entity accessor
        result = await table.ScannableEntitys.Scan(x => x.Age >= 21).ToListAsyncResult();
        
        // === Format String Style ===
        result = await table.Scan("age >= {0}", 21).ToListAsyncResult();
        
        // Format string on Entity accessor
        result = await table.ScannableEntitys.Scan("age >= {0}", 21).ToListAsyncResult();
            
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
    public async Task ToListAsyncResult_ScanOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);
        
        // === Pagination ===
        var result = await table.ScannableEntitys.Scan().Take(100).ToListAsyncResult();
        
        // StartAt for next page
        var lastKey = new Dictionary<string, AttributeValue>();
        result = await table.ScannableEntitys.Scan()
            .StartAt(lastKey)
            .ToListAsyncResult();
        
        // === ConsistentRead ===
        result = await table.ScannableEntitys.Scan()
            .UsingConsistentRead()
            .ToListAsyncResult();
        
        // === Projection ===
        result = await table.ScannableEntitys.Scan()
            .WithProjection("pk, age")
            .ToListAsyncResult();
        
        // === Combined options ===
        result = await table.ScannableEntitys.Scan(x => x.Age >= 21)
            .UsingConsistentRead()
            .WithProjection("pk, age")
            .Take(100)
            .ToListAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToCompositeEntityListAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);

        // === ToCompositeEntityListAsyncResult - list of composite entities ===
        var result = await table.Scan().ToCompositeEntityListAsyncResult();
        
        // Entity accessor
        result = await table.ScannableEntitys.Scan().ToCompositeEntityListAsyncResult();
        
        // With filter
        result = await table.ScannableEntitys.Scan(x => x.Age >= 21).ToCompositeEntityListAsyncResult();
        
        // With options
        result = await table.ScannableEntitys.Scan()
            .Take(100)
            .ToCompositeEntityListAsyncResult();
            
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entities = result.Value; // List<T>
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);
        var cancellationToken = new CancellationToken();

        // === ToListAsyncResult with cancellation token ===
        var result = await table.ScannableEntitys.Scan().ToListAsyncResult(cancellationToken);
        
        // ToCompositeEntityListAsyncResult with cancellation token
        var compositeListResult = await table.ScannableEntitys.Scan().ToCompositeEntityListAsyncResult(cancellationToken);
    }
}
