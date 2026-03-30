using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class PutApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllPutPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };

        // === Builder Pattern with Dictionary ===
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
        
        // === Builder Pattern with Entity ===
        // Generated Put with POCO object
        await table.Put(entity).PutAsync();
        
        // Entity accessor Put with POCO object
        await table.BasicPkEntitys.Put(entity).PutAsync();
        
        // === Convenience Methods ===
        // Generated PutAsync (table level)
        await table.PutAsync(entity);
        
        // Generated PutAsync on Entity accessor
        await table.BasicPkEntitys.PutAsync(entity);
        
        // === Condition Expressions ===
        // Lambda condition (Preferred) - create only if not exists
        await table.BasicPkEntitys.Put(entity)
            .Where(x => x.PartitionKey.AttributeNotExists())
            .PutAsync();
        
        // Format string condition
        await table.BasicPkEntitys.Put(entity)
            .Where("attribute_not_exists(pk)")
            .PutAsync();
        
        // Manual WithValue condition
        await table.BasicPkEntitys.Put(entity)
            .Where("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "expected")
            .PutAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllPutPatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        var entity = new BasicPkSkEntity { PartitionKey = "1234", SortKey = "test", TotalCount = 5 };

        // === Builder Pattern with Dictionary ===
        await table.Put().WithItem(new Dictionary<string, AttributeValue>()
        {
            { "pk", new AttributeValue { S = "1234" } },
            { "sk", new AttributeValue { S = "test" } },
            { "totalCount",  new AttributeValue { N = "5" } }
        }).PutAsync();
        
        // === Builder Pattern with Entity ===
        await table.Put(entity).PutAsync();
        await table.BasicPkSkEntitys.Put(entity).PutAsync();
        
        // === Convenience Methods ===
        await table.PutAsync(entity);
        await table.BasicPkSkEntitys.PutAsync(entity);
        
        // === Condition Expressions ===
        // Lambda condition - create only if not exists
        await table.BasicPkSkEntitys.Put(entity)
            .Where(x => x.PartitionKey.AttributeNotExists())
            .PutAsync();
        
        // Lambda condition - optimistic locking
        await table.BasicPkSkEntitys.Put(entity)
            .Where(x => x.TotalCount == 4)
            .PutAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Put_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new PutItemRequest
        {
            TableName = "basicPk",
            Item = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } },
                { "name", new AttributeValue { S = "Test" } }
            }
        };
        
        // Raw SDK builder pattern
        await table.Put<BasicPkEntity>(request).PutAsync();
        
        // Raw SDK convenience method
        await table.PutAsync<BasicPkEntity>(request);
    }
}