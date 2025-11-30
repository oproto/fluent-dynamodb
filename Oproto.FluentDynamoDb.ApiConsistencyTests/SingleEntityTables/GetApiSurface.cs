using Amazon.DynamoDBv2;
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

        // Manual Get request builder and WithKey
        var result = await table.Get<BasicPkEntity>().WithKey("pk", "1234").GetItemAsync();
        
        // Generated Get
        result = await table.Get("1234").GetItemAsync();

        // Generated Get on Entity accessor
        result = await table.BasicPkEntitys.Get("1234").GetItemAsync();
        
        // Generated GetAsync
        result = await table.GetAsync("1234");

        // Generated GetAsync on Entity accessor
        result = await table.BasicPkEntitys.GetAsync("1234");
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllGetPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // Manual Get request builder and WithKey
        var result = await table.Get<BasicPkSkEntity>().WithKey("pk", "1234", "sk", "test").GetItemAsync();
        
        // Generated Get
        result = await table.Get("1234","test").GetItemAsync();

    }
}