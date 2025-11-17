using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class PutApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllPutPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, null);

        // Manual Put WithItem with Attribute Values
        await table.Put().WithItem(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } }
        }).PutAsync();
        
        // Manual Put with Attribute Values
        await table.Put(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } }
        }).PutAsync();
        
        // Generated Put with POCO object
        await table.Put(new BasicPkEntity
        {
            PartitionKey = "1234"
        }).PutAsync();
        
        // Manual Put WithItem with Attribute Values on EntityAccessor
        await table.BasicPkEntitys.Put().WithItem(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } }
        }).PutAsync();
        
        // Manual Put with Attribute Values on EntityAccessor
        await table.BasicPkEntitys.Put(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } }
        }).PutAsync();
        
        // Generated Put with POCO object on EntityAccessor
        await table.BasicPkEntitys.Put(new BasicPkEntity
        {
            PartitionKey = "1234"
        }).PutAsync();

    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllPutPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, null);

        // Manual Put with Attribute Values
        await table.Put().WithItem(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } },
            { "sk", new AttributeValue { S = "test" } },
            { "totalCount",  new AttributeValue { N = "5" } }
        }).PutAsync();
        
        // Manual Put with Attribute Values
        await table.Put(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } },
            { "sk", new AttributeValue { S = "test" } },
            { "totalCount",  new AttributeValue { N = "5" } }
        }).PutAsync();
        
        // Generated Put with POCO object
        await table.Put(new BasicPkSkEntity
        {
            PartitionKey = "1234",
            SortKey = "test",
            TotalCount = 5
        }).PutAsync();

        // Manual Put with Attribute Values on EntityAccessor
        await table.BasicPkSkEntitys.Put().WithItem(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } },
            { "sk", new AttributeValue { S = "test" } },
            { "totalCount",  new AttributeValue { N = "5" } }
        }).PutAsync();
        
        // Manual Put with Attribute Values on EntityAccessor
        await table.BasicPkSkEntitys.Put(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } },
            { "sk", new AttributeValue { S = "test" } },
            { "totalCount",  new AttributeValue { N = "5" } }
        }).PutAsync();
        
        // Generated Put with POCO object on EntityAccessor
        await table.BasicPkSkEntitys.Put(new BasicPkSkEntity
        {
            PartitionKey = "1234",
            SortKey = "test",
            TotalCount = 5
        }).PutAsync();
        
    }
}