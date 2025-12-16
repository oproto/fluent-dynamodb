using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class UpdateApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllUpdatePatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Lambda Expression Style (Preferred) ===
        // Lambda expression update
        await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = 32 })
            .UpdateAsync();
        
        // Lambda expression update with increment
        await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = x.Age + 1 })
            .UpdateAsync();
        
        // Lambda expression update with condition
        await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = 32 })
            .Where(x => x.Name == "Test")
            .UpdateAsync();
        
        // === Format String Style ===
        // Format string update
        await table.Update("1234")
            .Set("SET age={0}", 32)
            .UpdateAsync();
        
        // Format string update with condition
        await table.Update("1234")
            .Set("SET age={0}", 32)
            .Where("name = {0}", "Test")
            .UpdateAsync();
        
        // === Manual WithValue Style ===
        // Manual update with explicit attributes and values
        await table.Update("1234")
            .Set("SET #age = :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", 32)
            .UpdateAsync();
        
        // Manual update with condition
        await table.Update("1234")
            .Set("SET #age = :age")
            .Where("#name = :name")
            .WithAttribute("#age", "age")
            .WithAttribute("#name", "name")
            .WithValue(":age", 32)
            .WithValue(":name", "Test")
            .UpdateAsync();
        
        // === Entity Accessor Patterns ===
        // Lambda on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = 32 })
            .UpdateAsync();
        
        // Lambda with condition on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = 32 })
            .Where(x => x.Name == "Test")
            .UpdateAsync();
        
        // Format string on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set("SET age={0}", 32)
            .UpdateAsync();
        
        // Format string with condition on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set("SET age={0}", 32)
            .Where("name = {0}", "Test")
            .UpdateAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllUpdatePatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Lambda Expression Style (Preferred) ===
        await table.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 5 })
            .UpdateAsync();
        
        // Lambda with increment
        await table.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = x.TotalCount + 1 })
            .UpdateAsync();
        
        // Lambda with condition
        await table.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 5 })
            .Where(x => x.TotalCount > 0)
            .UpdateAsync();
        
        // === Format String Style ===
        await table.Update("1234", "test")
            .Set("SET totalCount={0}", 5)
            .UpdateAsync();
        
        // === Manual WithValue Style ===
        await table.Update("1234", "test")
            .Set("SET #tc = :tc")
            .WithAttribute("#tc", "totalCount")
            .WithValue(":tc", 5)
            .UpdateAsync();
        
        // === Entity Accessor Patterns ===
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 5 })
            .UpdateAsync();
        
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 5 })
            .Where(x => x.TotalCount > 0)
            .UpdateAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Update_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new UpdateItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            },
            UpdateExpression = "SET #age = :age",
            ExpressionAttributeNames = new Dictionary<string, string> { { "#age", "age" } },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":age", new AttributeValue { N = "32" } }
            }
        };
        
        // Raw SDK builder pattern
        await table.Update<BasicPkEntity>(request).UpdateAsync();
        
        // Raw SDK convenience method
        await table.UpdateAsync<BasicPkEntity>(request);
    }
}