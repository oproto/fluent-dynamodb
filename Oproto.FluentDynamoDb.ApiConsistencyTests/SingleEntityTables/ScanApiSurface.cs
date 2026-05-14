using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class ScanApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllScanPatterns_ScannableTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);

        // === Basic Scan (no filter) ===
        var results = await table.Scan().ToListAsync();
        
        // Scan on Entity accessor
        results = await table.ScannableEntitys.Scan().ToListAsync();
        
        // === Lambda Expression Style (Preferred) ===
        results = await table.Scan(x => x.Age >= 21).ToListAsync();
        
        // Lambda on Entity accessor
        results = await table.ScannableEntitys.Scan(x => x.Age >= 21).ToListAsync();
        
        // === Format String Style ===
        results = await table.Scan("age >= {0}", 21).ToListAsync();
        
        // Format string on Entity accessor
        results = await table.ScannableEntitys.Scan("age >= {0}", 21).ToListAsync();
        
        // === Manual WithValue Style ===
        results = await table.Scan("#age >= :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", 21)
            .ToListAsync();
        
        // Manual on Entity accessor
        results = await table.ScannableEntitys.Scan("#age >= :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", 21)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ScanOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);
        
        // === Pagination ===
        // Take (limit)
        var scan = table.ScannableEntitys.Scan().Take(100);
        var results = await scan.ToListAsync();
        
        // Access LastEvaluatedKey from response after execution
        var lastKey = scan.Response?.LastEvaluatedKey ?? new Dictionary<string, AttributeValue>();
        
        // StartAt for next page (using LastEvaluatedKey from previous scan)
        results = await table.ScannableEntitys.Scan()
            .StartAt(lastKey)
            .ToListAsync();
        
        // === ConsistentRead ===
        results = await table.ScannableEntitys.Scan()
            .UsingConsistentRead()
            .ToListAsync();
        
        // === Projection ===
        results = await table.ScannableEntitys.Scan()
            .WithProjection("pk, age")
            .ToListAsync();
        
        // === Combined options ===
        results = await table.ScannableEntitys.Scan(x => x.Age >= 21)
            .UsingConsistentRead()
            .WithProjection("pk, age")
            .Take(100)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Scan_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new ScanRequest
        {
            TableName = "scannable",
            FilterExpression = "age >= :age",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":age", new AttributeValue { N = "21" } }
            }
        };
        
        // Raw SDK builder pattern
        var results = await table.Scan<ScannableEntity>(request).ToListAsync();
        
        // Raw SDK convenience method
        results = await table.ScanAsync<ScannableEntity>(request);
    }
}