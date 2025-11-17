using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class QueryApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllQueryPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, null);

        // Query-expression string based with explicit names
        var results = await table.Query("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "1234")
            .ToListAsync();

        // Query-expression string based with string formatting
        results = await table.Query("pk = {0}", "1234").ToListAsync();
        
        // Query-expression using c# Lambda Expression translation
        results = await table.Query(x => x.PartitionKey == "1234").ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllQueryPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client,null);

        // Query-expression string based with explicit names
        var results = await table.Query("#pk = :pk AND begins_with(#sk,:sk)")
            .WithAttribute("#pk", "pk")
            .WithAttribute("#sk", "sk")
            .WithValue(":pk", "1234")
            .WithValue(":sk", "test")
            .ToListAsync();
        
        // Query-expression string based with explicit names and filter expression
        results = await table.Query("#pk = :pk AND begins_with(#sk,:sk)")
            .WithFilter("#totalCount > :totalCount")
            .WithAttribute("#pk", "pk")
            .WithAttribute("#sk", "sk")
            .WithAttribute("#totalCount", "totalCount")
            .WithValue(":pk", "1234")
            .WithValue(":sk", "test")
            .WithValue(":totalCount", 5)
            .ToListAsync();

        // Query-expression string based with string formatting
        results = await table.Query("pk = {0} AND begins_with(sk,{1})", "1234", "test").ToListAsync();
        
        // Query-expression string based with string formatting and filter expression
        results = await table.Query("pk = {0} AND begins_with(sk,{1})", "1234", "test")
            .WithFilter("totalCount > {0}", 5)
            .ToListAsync();
        
        // Query-expression using c# Lambda Expression translation
        results = await table.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test")).ToListAsync();
        
        // Query-expression using c# Lambda Expression translation with Filter Expression
        results = await table.Query(x => x.PartitionKey == "1234" && x.SortKey.StartsWith("test"))
            .WithFilter(x => x.TotalCount > 5)
            .ToListAsync();
    }
}