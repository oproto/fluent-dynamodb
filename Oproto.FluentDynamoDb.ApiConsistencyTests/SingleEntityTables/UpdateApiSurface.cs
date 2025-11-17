using Amazon.DynamoDBv2;
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
        BasicPkTable table = new BasicPkTable(client, null);

        // String expression update 
        await table.Update("1234")
            .Set("SET age={0}", 32)
            .UpdateAsync();
        
        // String expression update with condition
        await table.Update("1234")
            .Set("SET age={0}", 32)
            .Where("name = {0}", "Test")
            .UpdateAsync();
        
        // c# lambda exression update
        await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel
            {
                Age = 32
            })
            .UpdateAsync();
        
        // c# lambda expression update and condition
        await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel
            {
                Age = 32
            })
            .Where(x => x.Name == "Test")
            .UpdateAsync();
        
        // String expression update on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set("SET age={0}", 32)
            .UpdateAsync();
        
        // String expression update with condition on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set("SET age={0}", 32)
            .Where("name = {0}", "Test")
            .UpdateAsync();
        
        // c# lambda exression update on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel
            {
                Age = 32
            })
            .UpdateAsync();
        
        // c# lambda expression update and condition on EntityAccessor
        await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel
            {
                Age = 32
            })
            .Where(x => x.Name == "Test")
            .UpdateAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task AllUpdatePatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, null);

        // Many of these need to be updated for proper field names once the correct methods are available
        // String expression update 
        await table.Update("1234", "test")
            .Set("SET totalCount={0}", 5)
            .UpdateAsync();
        
        // String expression update with condition
        await table.Update("1234", "test")
            .Set("SET totalCount={0}", 5)
            .Where("name = {0}", "Test")
            .UpdateAsync();
        
        // c# lambda exression update
        await table.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel
            {
                TotalCount = 5
            })
            .UpdateAsync();
        
        // c# lambda expression update and condition
        await table.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel
            {
                TotalCount = 5
            })
            .Where(x => x.TotalCount > 0)
            .UpdateAsync();
        
        // String expression update on EntityAccessor
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set("SET totalCount={0}", 5)
            .UpdateAsync();
        
        // String expression update with condition on EntityAccessor
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set("SET totalCount={0}", 5)
            .Where("name = {0}", "Test")
            .UpdateAsync();
        
        // c# lambda exression update on EntityAccessor
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel
            {
                TotalCount = 5
            })
            .UpdateAsync();
        
        // c# lambda expression update and condition on EntityAccessor
        await table.BasicPkSkEntitys.Update("1234", "test")
            .Set(x => new BasicPkSkEntityUpdateModel
            {
                TotalCount = 5
            })
            .Where(x => x.TotalCount > 0)
            .UpdateAsync();
    }
}