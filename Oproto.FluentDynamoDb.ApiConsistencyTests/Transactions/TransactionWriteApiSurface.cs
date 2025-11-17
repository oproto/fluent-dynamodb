using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Transactions;

public class TransactionWriteApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllTransactionWriteOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, null);

        var item1 = new BasicPkEntity()
        {
            PartitionKey = "0123",
            Age = 20,
            Name = "John Doe"
        };
        
        //
        // Transaction Write with String Expression Formatting
        await DynamoDbTransactions.Write
            .WithClientRequestToken("UniqueToken")
            .Add(table.Put(item1))
            .Add(table.Update("1234").Set("SET age={0}", 30))
            .Add(table.Delete("1235"))
            .Add(table.ConditionCheck("9999").Where("name={0}","Test"))
            .ExecuteAsync();
        
        //
        // Transaction Write with Lambda Expressions
        await DynamoDbTransactions.Write
            .WithClientRequestToken("UniqueToken")
            .Add(table.Put(item1))
            .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(table.Delete("1235"))
            .Add(table.ConditionCheck("9999").Where(x => x.Name == "Test"))
            .ExecuteAsync();
    
/* Equivalent to above
        var transactionWriteRequest = new TransactWriteItemsRequest()
        {
            ClientRequestToken = "UniqueToken",
            TransactItems = new List<TransactWriteItem>()
            {
                new()
                {
                    Put = new Put()
                    {
                        TableName = "basicpktable",
                        Item = new Dictionary<string, AttributeValue>()
                        {
                            { "pk", new AttributeValue { S = "0123" } },
                            { "name", new AttributeValue { S = "John Doe" } },
                            { "age", new AttributeValue { N = "20" } },
                        }
                    }
                },
                new()
                {
                    Update = new Update()
                    {
                        TableName = "basicpktable",
                        UpdateExpression = "SET #age=:age",
                        ExpressionAttributeNames = new Dictionary<string, string>()
                        {
                            { "#age", "age" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            { ":age", new AttributeValue { N = "30" } }
                        },
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            { "pk", new AttributeValue { S = "1234" } },
                        }
                    }
                },
                new()
                {
                    Delete = new Delete()
                    {
                        TableName = "basicpktable",
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            { "pk", new AttributeValue { S = "2345" } },
                        }
                    }
                },
                new ()
                {
                    ConditionCheck = new ConditionCheck()
                    {
                        TableName = "basicpktable",
                        Key = new Dictionary<string, AttributeValue>()
                        {
                            { "pk", new AttributeValue { S = "9999" } }
                        },
                        ConditionExpression = "#name = :name",
                        ExpressionAttributeNames = new Dictionary<string, string>()
                        {
                            { "#name", "name" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                        {
                            { ":name", new AttributeValue { S = "Test" } }
                        }
                    }
                }
            }
        };
        await table.DynamoDbClient.TransactWriteItemsAsync(transactionWriteRequest);
*/
    }
}