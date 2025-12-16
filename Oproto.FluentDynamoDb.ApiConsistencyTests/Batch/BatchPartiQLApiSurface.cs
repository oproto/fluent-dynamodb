using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Batch;

public class BatchPartiQLApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_SelectOperations_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsync() pattern ===
        var result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "2345"))
            .ExecuteAsync();

        // === Result access patterns ===
        var item1 = result.GetItem<BasicPkEntity>(0);
        var item2 = result.GetItem<BasicPkEntity>(1);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_ExecuteAndMapAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Single item ===
        var single = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .ExecuteAndMapAsync<BasicPkEntity>();

        // === Two items ===
        var (item1, item2) = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "2345"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity>();

        // === Three items ===
        var (a, b, c) = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "2345"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "3456"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_CrossTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Cross-table batch PartiQL ===
        var result = await DynamoDbBatch.PartiQL
            .Add(pkTable.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "pk1"))
            .Add(pkSkTable.ExecutePartiQL<BasicPkSkEntity>("SELECT * FROM basicPkSk WHERE pk = {0}", "pk2"))
            .ExecuteAsync();

        // === Cross-table with tuple mapping ===
        var (pkEntity, pkSkEntity) = await DynamoDbBatch.PartiQL
            .Add(pkTable.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "pk1"))
            .Add(pkSkTable.ExecutePartiQL<BasicPkSkEntity>("SELECT * FROM basicPkSk WHERE pk = {0}", "pk2"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkSkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_MixedOperations_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Mixed SELECT + UPDATE/DELETE operations ===
        var result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("UPDATE basicPk SET name = {0} WHERE pk = {1}", "NewName", "2345"))
            .Add(table.ExecutePartiQL<BasicPkEntity>("DELETE FROM basicPk WHERE pk = {0}", "3456"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_BuilderOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === WithClient ===
        var result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .WithClient(client)
            .ExecuteAsync();

        // === Pass client to ExecuteAsync ===
        result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = {0}", "1234"))
            .ExecuteAsync(client);
    }
}
