using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class QueryApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllQueryPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Lambda Expression Style (Preferred) ===
        var results = await table.Query(x => x.PartitionKey == "1234").ToListAsync();
        
        // Lambda on Entity accessor
        results = await table.BasicPkEntitys.Query(x => x.PartitionKey == "1234").ToListAsync();
        
        // === Format String Style ===
        results = await table.Query("pk = {0}", "1234").ToListAsync();
        
        // Format string on Entity accessor
        results = await table.BasicPkEntitys.Query("pk = {0}", "1234").ToListAsync();
        
        // === Manual WithValue Style ===
        results = await table.Query("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "1234")
            .ToListAsync();
        
        // Manual on Entity accessor
        results = await table.BasicPkEntitys.Query("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "1234")
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllQueryPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Lambda Expression Style (Preferred) ===
        var results = await table.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test")).ToListAsync();
        
        // Lambda with filter expression
        results = await table.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test"))
            .WithFilter(x => x.TotalCount > 5)
            .ToListAsync();
        
        // Lambda on Entity accessor
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test")).ToListAsync();
        
        // Lambda with filter on Entity accessor
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .WithFilter(x => x.TotalCount > 5)
            .ToListAsync();
        
        // === Format String Style ===
        results = await table.Query("pk = {0} AND begins_with(sk,{1})", "1234", "test").ToListAsync();
        
        // Format string with filter expression
        results = await table.Query("pk = {0} AND begins_with(sk,{1})", "1234", "test")
            .WithFilter("totalCount > {0}", 5)
            .ToListAsync();
        
        // === Manual WithValue Style ===
        results = await table.Query("#pk = :pk AND begins_with(#sk,:sk)")
            .WithAttribute("#pk", "pk")
            .WithAttribute("#sk", "sk")
            .WithValue(":pk", "1234")
            .WithValue(":sk", "test")
            .ToListAsync();
        
        // Manual with filter expression
        results = await table.Query("#pk = :pk AND begins_with(#sk,:sk)")
            .WithFilter("#totalCount > :totalCount")
            .WithAttribute("#pk", "pk")
            .WithAttribute("#sk", "sk")
            .WithAttribute("#totalCount", "totalCount")
            .WithValue(":pk", "1234")
            .WithValue(":sk", "test")
            .WithValue(":totalCount", 5)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task QueryOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        
        // === Pagination ===
        // Take (limit)
        var query = table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234").Take(25);
        var results = await query.ToListAsync();
        
        // Access LastEvaluatedKey from response after execution
        var lastKey = query.Response?.LastEvaluatedKey ?? new Dictionary<string, AttributeValue>();
        
        // StartAt for next page (using LastEvaluatedKey from previous query)
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .StartAt(lastKey)
            .ToListAsync();
        
        // === ConsistentRead ===
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .UsingConsistentRead()
            .ToListAsync();
        
        // === Projection ===
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .WithProjection("pk, sk, totalCount")
            .ToListAsync();
        
        // === ScanIndexForward (sort order) ===
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .ScanIndexForward(false)
            .ToListAsync();
        
        // === Combined options ===
        results = await table.BasicPkSkEntitys.Query(x => x.PartitionKey == "1234")
            .UsingConsistentRead()
            .ScanIndexForward(false)
            .WithProjection("pk, sk, totalCount")
            .Take(25)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Query_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new QueryRequest
        {
            TableName = "basicPkSk",
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = "1234" } }
            }
        };
        
        // Raw SDK builder pattern
        var results = await table.Query<BasicPkSkEntity>(request).ToListAsync();
        
        // Raw SDK convenience method
        results = await table.QueryAsync<BasicPkSkEntity>(request);
    }
}