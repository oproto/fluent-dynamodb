using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface tests for raw SDK request overloads.
/// These methods accept pre-built AWS SDK request objects for advanced scenarios.
/// </summary>
public class RawSdkApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_GetItem_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        var request = new GetItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            },
            ConsistentRead = true,
            ProjectionExpression = "pk, name, age"
        };
        
        // Builder pattern with raw request
        var result = await table.Get<BasicPkEntity>(request).GetItemAsync();
        
        // Convenience method with raw request
        result = await table.GetAsync<BasicPkEntity>(request);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_PutItem_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        var request = new PutItemRequest
        {
            TableName = "basicPk",
            Item = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } },
                { "name", new AttributeValue { S = "Test" } },
                { "age", new AttributeValue { N = "25" } }
            },
            ConditionExpression = "attribute_not_exists(pk)"
        };

        // Builder pattern with raw request
        await table.Put<BasicPkEntity>(request).PutAsync();
        
        // Convenience method with raw request
        await table.PutAsync<BasicPkEntity>(request);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_UpdateItem_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        var request = new UpdateItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            },
            UpdateExpression = "SET #name = :name, #age = :age",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                { "#name", "name" },
                { "#age", "age" }
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":name", new AttributeValue { S = "NewName" } },
                { ":age", new AttributeValue { N = "30" } }
            },
            ConditionExpression = "attribute_exists(pk)"
        };
        
        // Builder pattern with raw request
        await table.Update<BasicPkEntity>(request).UpdateAsync();
        
        // Convenience method with raw request
        await table.UpdateAsync<BasicPkEntity>(request);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_DeleteItem_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        var request = new DeleteItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            },
            ConditionExpression = "age < :age",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":age", new AttributeValue { N = "18" } }
            }
        };
        
        // Builder pattern with raw request
        await table.Delete<BasicPkEntity>(request).DeleteAsync();
        
        // Convenience method with raw request
        await table.DeleteAsync<BasicPkEntity>(request);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_Query_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);
        
        var request = new QueryRequest
        {
            TableName = "basicPkSk",
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
            FilterExpression = "totalCount > :count",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = "1234" } },
                { ":sk", new AttributeValue { S = "test" } },
                { ":count", new AttributeValue { N = "5" } }
            },
            Limit = 25,
            ScanIndexForward = false
        };
        
        // Builder pattern with raw request
        var results = await table.Query<BasicPkSkEntity>(request).ToListAsync();
        
        // Convenience method with raw request
        results = await table.QueryAsync<BasicPkSkEntity>(request);
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdk_Scan_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, "scannable", options: null);
        
        var request = new ScanRequest
        {
            TableName = "scannable",
            FilterExpression = "age >= :age",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":age", new AttributeValue { N = "21" } }
            },
            Limit = 100
        };
        
        // Builder pattern with raw request
        var results = await table.Scan<ScannableEntity>(request).ToListAsync();
        
        // Convenience method with raw request
        results = await table.ScanAsync<ScannableEntity>(request);
    }
}
