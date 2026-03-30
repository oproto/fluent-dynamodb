using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Transactions;

/// <summary>
/// API Surface validation tests for DynamoDB Transaction Write operations.
/// These tests validate that all expected API patterns compile correctly.
/// Requirements: 2.10
/// </summary>
public class TransactionWriteApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_AllOperationTypes_FormatString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity
        {
            PartitionKey = "0123",
            Age = 20,
            Name = "John Doe"
        };
        
        // === Transaction Write with Format String expressions ===
        await DynamoDbTransactions.Write
            .WithClientRequestToken("UniqueToken")
            .Add(table.Put(entity))
            .Add(table.Update("1234").Set("SET age={0}", 30))
            .Add(table.Delete("1235"))
            .Add(table.ConditionCheck("9999").Where("name={0}", "Test"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_AllOperationTypes_Lambda_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity
        {
            PartitionKey = "0123",
            Age = 20,
            Name = "John Doe"
        };
        
        // === Transaction Write with Lambda expressions ===
        await DynamoDbTransactions.Write
            .WithClientRequestToken("UniqueToken")
            .Add(table.Put(entity))
            .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(table.Delete("1235"))
            .Add(table.ConditionCheck("9999").Where(x => x.Name == "Test"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_PutWithCondition_BothStyles_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity
        {
            PartitionKey = "0123",
            Age = 20,
            Name = "John Doe"
        };

        // === Put with Lambda condition (create only) ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity).Where(x => x.PartitionKey.AttributeNotExists()))
            .ExecuteAsync();

        // === Put with Format String condition ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity).Where("attribute_not_exists(pk)"))
            .ExecuteAsync();

        // === Put with Manual condition ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity).Where("attribute_not_exists(#pk)").WithAttribute("#pk", "pk"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_UpdateWithCondition_BothStyles_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Update with Lambda condition ===
        await DynamoDbTransactions.Write
            .Add(table.Update("1234")
                .Set(x => new BasicPkEntityUpdateModel { Age = 30 })
                .Where(x => x.Age < 50))
            .ExecuteAsync();

        // === Update with Format String condition ===
        await DynamoDbTransactions.Write
            .Add(table.Update("1234")
                .Set("SET age={0}", 30)
                .Where("age < {0}", 50))
            .ExecuteAsync();

        // === Update with Manual condition ===
        await DynamoDbTransactions.Write
            .Add(table.Update("1234")
                .Set("SET #age = :newAge")
                .WithAttribute("#age", "age")
                .WithValue(":newAge", 30)
                .Where("#age < :maxAge")
                .WithValue(":maxAge", 50))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_DeleteWithCondition_BothStyles_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Delete with Lambda condition ===
        await DynamoDbTransactions.Write
            .Add(table.Delete("1234").Where(x => x.Age > 100))
            .ExecuteAsync();

        // === Delete with Format String condition ===
        await DynamoDbTransactions.Write
            .Add(table.Delete("1234").Where("age > {0}", 100))
            .ExecuteAsync();

        // === Delete with Manual condition ===
        await DynamoDbTransactions.Write
            .Add(table.Delete("1234")
                .Where("#age > :maxAge")
                .WithAttribute("#age", "age")
                .WithValue(":maxAge", 100))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_ConditionCheck_BothStyles_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ConditionCheck with Lambda ===
        await DynamoDbTransactions.Write
            .Add(table.ConditionCheck("9999").Where(x => x.Name == "Test"))
            .ExecuteAsync();

        // === ConditionCheck with Format String ===
        await DynamoDbTransactions.Write
            .Add(table.ConditionCheck("9999").Where("name = {0}", "Test"))
            .ExecuteAsync();

        // Note: ConditionCheckBuilder does not implement IWithAttributeNames/IWithAttributeValues,
        // so manual WithAttribute/WithValue style is not available. Use Lambda or Format String instead.
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_WithReturnConsumedCapacity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write with ReturnConsumedCapacity ===
        var response = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // === Access consumed capacity from response ===
        var consumedCapacity = response.ConsumedCapacity;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_WithReturnItemCollectionMetrics_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write with ReturnItemCollectionMetrics ===
        var response = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ReturnItemCollectionMetrics()
            .ExecuteAsync();

        // === Access item collection metrics from response ===
        var metrics = response.ItemCollectionMetrics;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_WithClientRequestToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write with idempotency token ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .WithClientRequestToken(Guid.NewGuid().ToString())
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_WithExplicitClient_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var customClient = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write with explicit client via WithClient ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .WithClient(customClient)
            .ExecuteAsync();

        // === Transaction Write with client passed to ExecuteAsync ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ExecuteAsync(client: customClient);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        var entity = new BasicPkSkEntity
        {
            PartitionKey = "pk1",
            SortKey = "sk1",
            TotalCount = 10
        };

        // === Transaction Write with PK+SK entity - Lambda style ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .Add(table.Update("pk1", "sk1").Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 20 }))
            .Add(table.Delete("pk2", "sk2"))
            .Add(table.ConditionCheck("pk3", "sk3").Where(x => x.TotalCount > 0))
            .ExecuteAsync();

        // === Transaction Write with PK+SK entity - Format String style ===
        await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .Add(table.Update("pk1", "sk1").Set("SET totalCount={0}", 20))
            .Add(table.Delete("pk2", "sk2"))
            .Add(table.ConditionCheck("pk3", "sk3").Where("totalCount > {0}", 0))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_MixedTables_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        var pkEntity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };
        var pkSkEntity = new BasicPkSkEntity { PartitionKey = "pk1", SortKey = "sk1", TotalCount = 10 };

        // === Transaction Write across multiple tables ===
        await DynamoDbTransactions.Write
            .Add(pkTable.Put(pkEntity))
            .Add(pkSkTable.Put(pkSkEntity))
            .Add(pkTable.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(pkSkTable.Delete("pk2", "sk2"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_RawSdkRequest_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        // === Direct SDK request execution ===
        var request = new TransactWriteItemsRequest
        {
            ClientRequestToken = "UniqueToken",
            TransactItems = new List<TransactWriteItem>
            {
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = "basicPk",
                        Item = new Dictionary<string, AttributeValue>
                        {
                            { "pk", new AttributeValue { S = "0123" } },
                            { "name", new AttributeValue { S = "John Doe" } },
                            { "age", new AttributeValue { N = "20" } }
                        }
                    }
                },
                new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = "basicPk",
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "pk", new AttributeValue { S = "1234" } }
                        },
                        UpdateExpression = "SET #age = :age",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            { "#age", "age" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":age", new AttributeValue { N = "30" } }
                        }
                    }
                },
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = "basicPk",
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "pk", new AttributeValue { S = "2345" } }
                        }
                    }
                },
                new TransactWriteItem
                {
                    ConditionCheck = new ConditionCheck
                    {
                        TableName = "basicPk",
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "pk", new AttributeValue { S = "9999" } }
                        },
                        ConditionExpression = "#name = :name",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            { "#name", "name" }
                        },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            { ":name", new AttributeValue { S = "Test" } }
                        }
                    }
                }
            },
            ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
        };

        var response = await DynamoDbTransactions.WriteAsync(client, request);
        var consumedCapacity = response.ConsumedCapacity;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_EntityAccessorPattern_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write using entity accessor ===
        await DynamoDbTransactions.Write
            .Add(table.BasicPkEntitys.Put(entity))
            .Add(table.BasicPkEntitys.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(table.BasicPkEntitys.Delete("2345"))
            .Add(table.BasicPkEntitys.ConditionCheck("9999").Where(x => x.Name == "Test"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_ComplexUpdateExpressions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Update with increment ===
        await DynamoDbTransactions.Write
            .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = x.Age + 1 }))
            .ExecuteAsync();

        // === Update with multiple fields ===
        await DynamoDbTransactions.Write
            .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30, Name = "Updated" }))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_AllBuilderOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };

        // === Transaction Write with all builder options ===
        var response = await DynamoDbTransactions.Write
            .WithClientRequestToken("idempotency-token")
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ReturnItemCollectionMetrics()
            .Add(table.Put(entity))
            .ExecuteAsync();
    }
}