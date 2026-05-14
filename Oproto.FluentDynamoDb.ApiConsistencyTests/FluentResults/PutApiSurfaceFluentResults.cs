using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Put operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.2, 4.2
/// </summary>
public class PutApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncResult_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };

        // === PutAsyncResult on builder ===
        var result = await table.Put(entity).PutAsyncResult();
        
        // Entity accessor Put
        result = await table.BasicPkEntitys.Put(entity).PutAsyncResult();
        
        // === Condition Expressions with PutAsyncResult ===
        // Lambda condition (Preferred) - create only if not exists (via entity accessor)
        result = await table.BasicPkEntitys.Put(entity)
            .Where(x => x.PartitionKey.AttributeNotExists())
            .PutAsyncResult();
        
        // Format string condition
        result = await table.BasicPkEntitys.Put(entity)
            .Where("attribute_not_exists(pk)")
            .PutAsyncResult();
            
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
    public async Task PutAsyncResult_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        var entity = new BasicPkSkEntity { PartitionKey = "1234", SortKey = "test", TotalCount = 5 };

        // === PutAsyncResult with PK+SK entity ===
        var result = await table.Put(entity).PutAsyncResult();
        result = await table.BasicPkSkEntitys.Put(entity).PutAsyncResult();
        
        // === Condition Expressions ===
        // Lambda condition - create only if not exists (via entity accessor)
        result = await table.BasicPkSkEntitys.Put(entity)
            .Where(x => x.PartitionKey.AttributeNotExists())
            .PutAsyncResult();
        
        // Lambda condition - optimistic locking (via entity accessor)
        result = await table.BasicPkSkEntitys.Put(entity)
            .Where(x => x.TotalCount == 4)
            .PutAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncResult_WithBlobProvider_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };
        var blobProvider = Substitute.For<IBlobStorageProvider>();

        // === PutAsyncResult with blob provider overload ===
        var result = await table.Put(entity).PutAsyncResult(blobProvider);
        
        // Entity accessor with blob provider
        result = await table.BasicPkEntitys.Put(entity).PutAsyncResult(blobProvider);
        
        // With condition and blob provider (via entity accessor)
        result = await table.BasicPkEntitys.Put(entity)
            .Where(x => x.PartitionKey.AttributeNotExists())
            .PutAsyncResult(blobProvider);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };
        var cancellationToken = new CancellationToken();

        // === PutAsyncResult with cancellation token ===
        var result = await table.BasicPkEntitys.Put(entity).PutAsyncResult(cancellationToken);
        
        // With blob provider and cancellation token
        var blobProvider = Substitute.For<IBlobStorageProvider>();
        result = await table.BasicPkEntitys.Put(entity).PutAsyncResult(blobProvider, cancellationToken);
    }
}
