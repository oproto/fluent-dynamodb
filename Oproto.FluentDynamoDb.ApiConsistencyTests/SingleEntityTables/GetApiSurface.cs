using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class GetApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllGetPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Builder Pattern ===
        // Manual Get request builder and WithKey
        var result = await table.Get<BasicPkEntity>().WithKey("pk", "1234").GetItemAsync();
        
        // Generated Get with key
        result = await table.Get("1234").GetItemAsync();

        // Generated Get on Entity accessor
        result = await table.BasicPkEntitys.Get("1234").GetItemAsync();
        
        // === Convenience Methods ===
        // Generated GetAsync (table level)
        result = await table.GetAsync("1234");

        // Generated GetAsync on Entity accessor
        result = await table.BasicPkEntitys.GetAsync("1234");
        
        // === Builder Options ===
        // With ConsistentRead
        result = await table.BasicPkEntitys.Get("1234").UsingConsistentRead().GetItemAsync();
        
        // With Projection
        result = await table.BasicPkEntitys.Get("1234").WithProjection("name, age").GetItemAsync();
        
        // Combined options
        result = await table.BasicPkEntitys.Get("1234")
            .UsingConsistentRead()
            .WithProjection("name, age")
            .GetItemAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllGetPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Builder Pattern ===
        // Manual Get request builder and WithKey (PK + SK)
        var result = await table.Get<BasicPkSkEntity>().WithKey("pk", "1234", "sk", "test").GetItemAsync();
        
        // Generated Get with PK + SK
        result = await table.Get("1234", "test").GetItemAsync();
        
        // Generated Get on Entity accessor with PK + SK
        result = await table.BasicPkSkEntitys.Get("1234", "test").GetItemAsync();
        
        // === Convenience Methods ===
        // Generated GetAsync (table level) with PK + SK
        result = await table.GetAsync("1234", "test");
        
        // Generated GetAsync on Entity accessor with PK + SK
        result = await table.BasicPkSkEntitys.GetAsync("1234", "test");
        
        // === Builder Options ===
        // With ConsistentRead
        result = await table.BasicPkSkEntitys.Get("1234", "test").UsingConsistentRead().GetItemAsync();
        
        // With Projection
        result = await table.BasicPkSkEntitys.Get("1234", "test").WithProjection("totalCount").GetItemAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Get_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new GetItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            }
        };
        
        // Raw SDK builder pattern
        var result = await table.Get<BasicPkEntity>(request).GetItemAsync();
        
        // Raw SDK convenience method
        result = await table.GetAsync<BasicPkEntity>(request);
    }
}