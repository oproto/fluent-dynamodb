using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Query operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.5, 3.1, 3.2, 4.3
/// </summary>
public class QueryApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Lambda Expression Style (Preferred) ===
        var result = await table.Query(x => x.PartitionKey == "1234").ToListAsyncResult();
        
        // Lambda on Entity accessor
        result = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToListAsyncResult();
        
        // === Format String Style ===
        result = await table.Query("pk = {0}", "1234").ToListAsyncResult();
        
        // Format string on Entity accessor
        result = await table.BasicPkEntitys.Query("pk = {0}", "1234").ToListAsyncResult();
            
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
    public async Task ToListAsyncResult_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Lambda Expression Style (Preferred) ===
        var result = await table.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test")).ToListAsyncResult();
        
        // Lambda on Entity accessor
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test")).ToListAsyncResult();
        
        // === Format String Style ===
        result = await table.Query("pk = {0} AND begins_with(sk,{1})", "1234", "test").ToListAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_QueryOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        
        // === Pagination ===
        var result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .Take(25)
            .ToListAsyncResult();
        
        // StartAt for next page
        var lastKey = new Dictionary<string, AttributeValue>();
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .StartAt(lastKey)
            .ToListAsyncResult();
        
        // === ConsistentRead ===
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .UsingConsistentRead()
            .ToListAsyncResult();
        
        // === Projection ===
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .WithProjection("pk, sk, totalCount")
            .ToListAsyncResult();
        
        // === ScanIndexForward (sort order) ===
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .ScanIndexForward(false)
            .ToListAsyncResult();
        
        // === Combined options ===
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .UsingConsistentRead()
            .ScanIndexForward(false)
            .WithProjection("pk, sk, totalCount")
            .Take(25)
            .ToListAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_WithBlobProvider_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var blobProvider = Substitute.For<IBlobStorageProvider>();

        // === ToListAsyncResult with blob provider overload ===
        var result = await table.Query(x => x.PartitionKey == "1234").ToListAsyncResult(blobProvider);
        
        // Entity accessor with blob provider
        result = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToListAsyncResult(blobProvider);
        
        // With options and blob provider
        result = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234")
            .Take(25)
            .ToListAsyncResult(blobProvider);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToCompositeEntityAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ToCompositeEntityAsyncResult - single composite entity ===
        var result = await table.Query(x => x.PartitionKey == "1234").ToCompositeEntityAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234").ToCompositeEntityAsyncResult();
        
        // With options
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .UsingConsistentRead()
            .ToCompositeEntityAsyncResult();
            
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entity = result.Value; // T? - may be null
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ToCompositeEntityListAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ToCompositeEntityListAsyncResult - list of composite entities ===
        var result = await table.Query(x => x.PartitionKey == "1234").ToCompositeEntityListAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234").ToCompositeEntityListAsyncResult();
        
        // With options
        result = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
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
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var cancellationToken = new CancellationToken();

        // === ToListAsyncResult with cancellation token ===
        var result = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToListAsyncResult(cancellationToken);
        
        // With blob provider and cancellation token
        var blobProvider = Substitute.For<IBlobStorageProvider>();
        result = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToListAsyncResult(blobProvider, cancellationToken);
        
        // ToCompositeEntityAsyncResult with cancellation token
        var compositeResult = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToCompositeEntityAsyncResult(cancellationToken);
        
        // ToCompositeEntityListAsyncResult with cancellation token
        var compositeListResult = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToCompositeEntityListAsyncResult(cancellationToken);
    }
}
